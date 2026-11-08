using System;
namespace SegmentUI
{
    /// <summary>按事件记录的有界诊断缓冲；关闭时不记录逐帧状态。</summary>
    public sealed class SegmentDiagnostics
    {
        public bool Enabled { get; set; }
        readonly string[] entries;
        int next, count;
        public SegmentDiagnostics(int capacity = 128)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            entries = new string[capacity];
        }
        public void Record(string message)
        {
            if (!Enabled) return;
            entries[next] = message;
            next = (next + 1) % entries.Length;
            count = Math.Min(count + 1, entries.Length);
        }
        public string[] Snapshot()
        {
            var result = new string[count];
            int start = (next - count + entries.Length) % entries.Length;
            for (int i = 0; i < count; i++) result[i] = entries[(start + i) % entries.Length];
            return result;
        }
    }
}
