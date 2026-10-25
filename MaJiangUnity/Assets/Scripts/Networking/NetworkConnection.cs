using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MaJiangLib
{
    /// <summary>单读任务、单写任务的TCP连接。回调不操作Unity或房间状态。</summary>
    public sealed class NetworkConnection
    {
        private readonly TcpClient client;
        private readonly CancellationTokenSource cancellation = new();
        private readonly Queue<NetworkMessage> outgoing = new();
        private readonly SemaphoreSlim outgoingSignal = new(0);
        private readonly object sendLock = new();
        private readonly Func<NetworkConnection, NetworkMessage, bool> onMessage;
        private readonly Action<NetworkConnection, Exception> onClosed;
        private readonly int queueCapacity;
        private readonly TimeSpan operationTimeout;
        private uint nextMessageId;
        private int closed;
        private long lastReceivedTimestamp = Stopwatch.GetTimestamp();
        private readonly long createdTimestamp = Stopwatch.GetTimestamp();

        public Guid Id { get; } = Guid.NewGuid();
        public bool IsClosed => Volatile.Read(ref closed) != 0;
        public Task Completion { get; }
        public double SecondsSinceReceive => (Stopwatch.GetTimestamp() - Interlocked.Read(ref lastReceivedTimestamp)) / (double)Stopwatch.Frequency;
        public double AgeSeconds => (Stopwatch.GetTimestamp() - createdTimestamp) / (double)Stopwatch.Frequency;

        public NetworkConnection(TcpClient client, Func<NetworkConnection, NetworkMessage, bool> onMessage, Action<NetworkConnection, Exception> onClosed,
            int queueCapacity = 64, TimeSpan? operationTimeout = null)
        {
            if (queueCapacity < 1) { throw new ArgumentOutOfRangeException(nameof(queueCapacity)); }
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.onMessage = onMessage ?? throw new ArgumentNullException(nameof(onMessage));
            this.onClosed = onClosed;
            this.queueCapacity = queueCapacity;
            this.operationTimeout = operationTimeout ?? TimeSpan.FromSeconds(30);
            client.NoDelay = true;
            Completion = Task.WhenAll(Task.Run(ReadLoopAsync), Task.Run(WriteLoopAsync));
            _ = Completion.ContinueWith(task =>
            {
                cancellation.Dispose();
                outgoingSignal.Dispose();
            }, TaskScheduler.Default);
        }

        /// <summary>复制并排队整个消息。队列满时关闭连接，不启动并行写操作。</summary>
        public uint Send(RoomMessageType type, byte[] payload)
        {
            if (payload == null || payload.Length > NetworkFrameCodec.MaximumMessageSize
                || !Enum.IsDefined(typeof(RoomMessageType), type))
            {
                throw new ArgumentException("Invalid outgoing message.", nameof(payload));
            }
            lock (sendLock)
            {
                if (IsClosed) { return 0; }
                if (outgoing.Count < queueCapacity)
                {
                    nextMessageId = nextMessageId == uint.MaxValue ? 1 : nextMessageId + 1;
                    outgoing.Enqueue(new NetworkMessage(type, nextMessageId, (byte[])payload.Clone()));
                    outgoingSignal.Release();
                    return nextMessageId;
                }
            }
            Close(new IOException("Outgoing message queue is full."));
            return 0;
        }

        public void Close(Exception reason = null)
        {
            lock (sendLock)
            {
                if (IsClosed) { return; }
                Volatile.Write(ref closed, 1);
                outgoing.Clear();
            }
            cancellation.Cancel();
            client.Close();
            onClosed?.Invoke(this, reason);
        }

        public async Task CloseAsync()
        {
            Close();
            await Completion.ConfigureAwait(false);
        }

        private async Task ReadLoopAsync()
        {
            try
            {
                NetworkStream stream = client.GetStream();
                NetworkMessageAssembler assembler = new();
                byte[] frame = new byte[NetworkFrameCodec.FrameSize];
                using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
                deadline.CancelAfter(operationTimeout);
                while (!cancellation.IsCancellationRequested)
                {
                    bool hasFrame = await ReadFrameAsync(stream, frame, deadline.Token).ConfigureAwait(false);
                    if (!hasFrame)
                    {
                        if (assembler.HasPartialMessage) { throw new EndOfStreamException("EOF during fragmented message."); }
                        break;
                    }
                    NetworkMessage message = assembler.AddFrame(frame);
                    Interlocked.Exchange(ref lastReceivedTimestamp, Stopwatch.GetTimestamp());
                    if (message != null && !onMessage(this, message))
                    {
                        throw new IOException("Incoming message queue is full.");
                    }
                    if (message != null) { deadline.CancelAfter(operationTimeout); }
                }
                Close();
            }
            catch (Exception exception)
            {
                Close(exception);
            }
        }

        private async Task WriteLoopAsync()
        {
            try
            {
                NetworkStream stream = client.GetStream();
                while (!cancellation.IsCancellationRequested)
                {
                    await outgoingSignal.WaitAsync(cancellation.Token).ConfigureAwait(false);
                    NetworkMessage message;
                    lock (sendLock)
                    {
                        if (outgoing.Count == 0) { continue; }
                        message = outgoing.Dequeue();
                    }
                    using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
                    deadline.CancelAfter(operationTimeout);
                    foreach (byte[] frame in NetworkFrameCodec.Encode(message))
                    {
                        await stream.WriteAsync(frame, 0, frame.Length, deadline.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception exception)
            {
                Close(exception);
            }
        }

        /// <summary>返回false只表示帧边界上的EOF。半帧EOF必须报错。</summary>
        public static async Task<bool> ReadFrameAsync(Stream stream, byte[] frame, CancellationToken cancellationToken)
        {
            if (frame.Length != NetworkFrameCodec.FrameSize) { throw new ArgumentException("Frame buffer must be 1024 bytes.", nameof(frame)); }
            int offset = 0;
            while (offset < frame.Length)
            {
                int count = await stream.ReadAsync(frame, offset, frame.Length - offset, cancellationToken).ConfigureAwait(false);
                if (count == 0)
                {
                    if (offset == 0) { return false; }
                    throw new EndOfStreamException("EOF inside a fixed frame.");
                }
                offset += count;
            }
            return true;
        }
    }
}
