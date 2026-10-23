using SnapBerry.Core;
using Xunit;
namespace SnapBerry.Core.Tests;
// 문서 좌표·번호 순서·명령 실행 취소 등 UI와 독립적인 편집 규칙을 검증한다.
public class DocumentTests
{
    [Theory][InlineData(1,"a")][InlineData(26,"z")][InlineData(27,"aa")][InlineData(52,"az")][InlineData(53,"ba")]
    public void AlphabeticSequenceContinuesAfterZ(long value,string label) => Assert.Equal(label, StepLabelFormatter.Format(value, StepSequenceMode.Alphabetic));
    [Fact] public void UndoInsertionRestoresCounter()
    {
        var doc = new AnnotationDocument(100,100); var history = new CommandHistory(doc);
        history.InsertStep(new(10,10), new());
        Assert.Equal("1", doc.Annotations.Single().Text); Assert.Equal(2,doc.Sequence.NextNumeric);
        history.Undo(); Assert.Empty(doc.Annotations); Assert.Equal(1,doc.Sequence.NextNumeric);
        history.Redo(); Assert.Equal("1",doc.Annotations.Single().Text); Assert.Equal(2,doc.Sequence.NextNumeric);
    }
    [Fact] public void SwitchingModesKeepsIndependentCounters()
    {
        var doc=new AnnotationDocument(100,100);var history=new CommandHistory(doc);
        history.InsertStep(new(0,0),new());history.InsertStep(new(0,0),new());
        history.SetSequence(doc.Sequence with { Mode=StepSequenceMode.Alphabetic });history.InsertStep(new(0,0),new());
        history.SetSequence(doc.Sequence with { Mode=StepSequenceMode.Numeric });history.InsertStep(new(0,0),new());
        Assert.Equal(new[]{"1","2","a","3"},doc.Annotations.Select(a=>a.Text));
    }
    [Fact] public void ExportDoesNotMarkPersisted()
    {
        var d=new AnnotationDocument(100,100);var h=new CommandHistory(d);h.Add(new(){Kind=AnnotationKind.Text,Text="x"});
        h.MarkExported(h.CurrentStateToken);Assert.False(h.IsDirty);Assert.True(h.IsPersistenceDirty);
        h.MarkPersisted(h.CurrentStateToken);Assert.False(h.IsPersistenceDirty);
    }
    [Fact] public void SaveCompletionDoesNotMarkLaterEditPersisted()
    {
        var d=new AnnotationDocument(100,100);var h=new CommandHistory(d);h.Add(new(){Text="x"});var old=h.CurrentStateToken;
        h.Add(new(){Text="y"});h.MarkPersisted(old);Assert.True(h.IsPersistenceDirty);
    }
    [Fact] public void NewBranchAtSameDepthIsDirty()
    {
        var d=new AnnotationDocument(100,100);var h=new CommandHistory(d);h.Add(new(){Text="a"});h.MarkPersisted(h.CurrentStateToken);
        h.Undo();h.Add(new(){Text="b"});Assert.True(h.IsPersistenceDirty);Assert.False(h.CanRedo);
    }
    [Fact] public void NegativeDesktopOriginTranslatesCrop()
    {
        var rect=PixelRect.FromPoints(new(-1900,-1000),new(-1800,-950));
        Assert.Equal(new PixelRect(20,80,100,50),rect.Translate(1920,1080));
    }
    [Fact] public void ViewTransformRoundTrip()
    {
        foreach(var zoom in new[]{0.5,1,2}){var t=new ViewTransform(zoom,20,30);var p=new PixelPoint(11,15);Assert.Equal(p,t.ToImage(t.ToView(p)));}
    }
}
