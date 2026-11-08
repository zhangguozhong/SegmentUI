using NUnit.Framework;
namespace SegmentUI.Tests
{
    public class SegmentDiagnosticsTests
    {
        [Test] public void BufferRetainsOnlyLatestEventsInChronologicalOrder()
        {
            var log=new SegmentDiagnostics(4){Enabled=true};
            for(int i=0;i<10;i++)log.Record("event"+i);
            CollectionAssert.AreEqual(new[]{"event6","event7","event8","event9"},log.Snapshot());
            log.Enabled=false;log.Record("ignored");Assert.That(log.Snapshot().Length,Is.EqualTo(4));
        }
    }
}
