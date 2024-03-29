﻿namespace Tellurian.Trains.LocoNetMonitor.LocoNet;
internal class MessagePriorityQueue
{
    readonly List<QueuedMessage> _queue = new(100);
    public void AddOrUpdate(byte[] locoNetMessage)
    {
        if (locoNetMessage is null || locoNetMessage.Length < 2) return;
        var queuedMessage = new QueuedMessage(locoNetMessage);
        if (queuedMessage.OperationsCode == 0xA0)
        {
            lock (_queue)
            {
                var index = _queue.IndexOf(queuedMessage);
                if (index >= 0) _queue[index] = queuedMessage;
                else _queue.Add(queuedMessage);
            }
        }
        else
        {
            _queue.Add(queuedMessage);
        }
    }

    public byte[] TryGetNextMessage()
    {
        lock (_queue)
        {
            if (_queue.Count == 0) return [];
            var item = _queue.OrderBy(item => item.Priority).First();
            _queue.Remove(item);
            return item.Data;
        }
    }
}

public sealed class QueuedMessage(byte[] data)
{
    public byte[] Data { get; } = data;
    public byte OperationsCode => Data[0];
    public byte SlotNumber => Data.SlotNumber();

    private readonly DateTimeOffset Timestamp = DateTimeOffset.Now;
    private TimeSpan Delay => DateTimeOffset.Now - Timestamp;
    public int Priority => OperationsCodePriority - Delay.Milliseconds;
    public override bool Equals(object? obj) => obj is QueuedMessage qm && qm.OperationsCode == OperationsCode && qm.SlotNumber == SlotNumber;
    public override int GetHashCode() => HashCode.Combine(OperationsCode, SlotNumber);

    private int OperationsCodePriority => OperationsCode switch
    {
        0xA0 => 10,
        0xA1 => 20,
        0xA2 => 30,
        0xA3 => 40,
        0xD4 => 50,
        _ => 0,
    };
}
