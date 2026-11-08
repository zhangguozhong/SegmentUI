using System;
namespace SegmentUI
{
    /// <summary>按事件记录的有界诊断缓冲；关闭时不记录逐帧状态。</summary>
    public sealed class SegmentDiagnostics
    {
        /// <summary>显式开启事件记录；关闭后 Record 直接返回。</summary>
        public bool Enabled { get; set; }
        readonly string[] entries;
        int next, count;
        /// <summary>创建固定容量环形缓冲，容量必须为正。</summary>
        public SegmentDiagnostics(int capacity = 128)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            entries = new string[capacity];
        }
        /// <summary>写入一条事件，容量满时覆盖最旧记录。</summary>
        public void Record(string message)
        {
            if (!Enabled) return;
            entries[next] = message;
            next = (next + 1) % entries.Length;
            count = Math.Min(count + 1, entries.Length);
        }
        /// <summary>按时间从旧到新返回副本，调用方修改数组不会影响内部缓冲。</summary>
        public string[] Snapshot()
        {
            var result = new string[count];
            int start = (next - count + entries.Length) % entries.Length;
            for (int i = 0; i < count; i++) result[i] = entries[(start + i) % entries.Length];
            return result;
        }
    }
}
