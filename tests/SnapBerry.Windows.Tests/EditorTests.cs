using SnapBerry.Core;
using SnapBerry.App.Editor;
using SnapBerry.App.Library;
using SnapBerry.App.Export;
using SnapBerry.App.Stickers;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 선택 객체의 텍스트·스타일 편집과 번호 변경에 따른 다음 순번 갱신을 검증한다.
public class EditorTests
{
    [Theory] [InlineData(StepSequenceMode.Numeric,"4","1","2")] [InlineData(StepSequenceMode.Alphabetic,"d","a","b")] [InlineData(StepSequenceMode.Alphabetic,"z","z","aa")] [InlineData(StepSequenceMode.UppercaseAlphabetic,"D","A","B")] [InlineData(StepSequenceMode.UppercaseAlphabetic,"Z","Z","AA")] [InlineData(StepSequenceMode.UppercaseAlphabetic,"AZ","AZ","BA")]
    public void EditedStepContinuesFromEditedValueAndUndoRestoresBoth(StepSequenceMode mode,string original,string edited,string next)=>Sta.Run(()=>
    {
        var m=Model();m.History!.SetSequence(new(){Mode=mode,NextNumeric=5,NextAlphabetic=5,NextUppercaseAlphabetic=5});var a=new Annotation{Kind=AnnotationKind.StepMarker,Text=original};m.History.Add(a);var sequence=m.Document!.Sequence;
        m.UpdateText(a.Id,edited);m.History.Undo();Assert.Equal(original,m.Document.Annotations.Single().Text);Assert.Equal(sequence,m.Document.Sequence);
        m.History.Redo();Assert.Equal(edited,m.Document.Annotations.Single().Text);m.History.InsertStep(new(30,30),new());Assert.Equal(next,m.Document.Annotations.Last().Text);
    });
    [Fact] public void InvalidStepEditDoesNotChangeLabelOrCounter()=>Sta.Run(()=>
    {var m=Model();var a=new Annotation{Kind=AnnotationKind.StepMarker,Text="4"};m.History!.Add(a);var sequence=m.Document!.Sequence;var token=m.History.CurrentStateToken;Assert.ThrowsAny<Exception>(()=>m.UpdateText(a.Id,"0"));Assert.Equal(a,m.Document.Annotations.Single());Assert.Equal(sequence,m.Document.Sequence);Assert.Equal(token,m.History.CurrentStateToken);});
    [Fact] public void FailedOpenPreservesCurrentIdentityAndDocument()=>Sta.Run(()=>
    {var m=Model();var id=m.CaptureId;var doc=m.Document;var png=m.Snapshot().OriginalPng[..24];Assert.ThrowsAny<Exception>(()=>m.Open(new(){Width=100,Height=100,OriginalPng=png}));Assert.Equal(id,m.CaptureId);Assert.Same(doc,m.Document);});
    [Fact] public void EditingTextRecomputesSelectableBounds()=>Sta.Run(()=>
    {var m=Model();var a=new Annotation{Kind=AnnotationKind.Text,Text="x",X=10,Y=10,Width=10,Height=20};m.History!.Add(a);m.UpdateText(a.Id,"A much longer label\nSecond line");var updated=m.Document!.Annotations.Single();Assert.True(updated.Width>100);Assert.True(updated.Height>20);Assert.NotNull(AnnotationHitTester.Hit(m.Document.Annotations,new(updated.X+updated.Width-5,updated.Y+5),0));});
    [Fact] public void ColorEditPreservesSelectedFontAndThickness()=>Sta.Run(()=>
    {var m=Model();var a=new Annotation{Kind=AnnotationKind.Text,Text="hello",Style=new(){FontSize=48,Thickness=8}};m.History!.Add(a);m.SelectedId=a.Id;m.UpdateSelectedStyle(s=>s with{Color="#139887"});var style=m.Document!.Annotations.Single().Style;Assert.Equal(48,style.FontSize);Assert.Equal(8,style.Thickness);Assert.Equal("#139887",style.Color);});
    private static EditorViewModel Model(){var m=new EditorViewModel();m.Open(new(){Width=100,Height=100,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(100,100))});return m;}
    [Fact] public void TopmostAnnotationIsSelected()=>Sta.Run(()=>{var m=Model();m.History!.Add(new(){X=10,Y=10,Width=40,Height=40});m.History.Add(new(){X=10,Y=10,Width=40,Height=40});Assert.Equal(m.Document!.Annotations[1].Id,AnnotationHitTester.Hit(m.Document.Annotations,new(20,20),2)!.Id);});
    [Fact] public void ReopenContinuesBothStepSequences()=>Sta.Run(()=>{var m=Model();m.History!.InsertStep(new(10,10),new());m.History.SetSequence(m.Document!.Sequence with{Mode=StepSequenceMode.Alphabetic});m.History.InsertStep(new(30,30),new());var r=m.Snapshot();var reopened=new EditorViewModel();reopened.Open(r);Assert.False(reopened.History!.CanUndo);Assert.Equal(2,reopened.Document!.Sequence.NextNumeric);Assert.Equal(2,reopened.Document.Sequence.NextAlphabetic);});
    [Fact] public void KeycapLabelUpdatesOnlySelectedSticker()=>Sta.Run(()=>{var m=Model();var d=StickerCatalog.Load().First(x=>x.IsKeycap);m.InsertSticker(d,new(10,10));m.InsertSticker(d,new(40,40));var before=m.Document!.Annotations[0];m.UpdateKeycapLabel(m.Document.Annotations[1].Id,"Ctrl+C");Assert.Equal(before,m.Document.Annotations[0]);Assert.Equal("Ctrl+C",m.Document.Annotations[1].Text);m.History!.Undo();Assert.Equal(d.Label,m.Document.Annotations[1].Text);});
}
