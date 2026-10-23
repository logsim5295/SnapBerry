using SnapBerry.Core;
using SnapBerry.Desktop.Editing;
using SnapBerry.Desktop.Rendering;
using Xunit;

namespace SnapBerry.Desktop.Tests;

public class EditingParityTests
{
    [Fact] public void EditedStepLabelAdvancesMatchingSequenceAndUndoesTogether()
    {
        using var editor=new DesktopEditor();editor.Open(EditorTests.Record());editor.InsertStep(new(3,4));
        editor.EditSelectedText("Z");
        Assert.Equal("Z",editor.Selected!.Text);Assert.Equal(StepSequenceMode.UppercaseAlphabetic,editor.Document!.Sequence.Mode);
        editor.InsertStep(new(20,4));Assert.Equal("AA",editor.Selected!.Text);
        editor.History!.Undo();editor.SelectedId=editor.Document.Annotations[0].Id;editor.History.Undo();
        Assert.Equal("1",editor.Selected!.Text);Assert.Equal(StepSequenceMode.Numeric,editor.Document.Sequence.Mode);Assert.Equal(2,editor.Document.Sequence.NextNumeric);
    }
    [Theory] [InlineData("0")] [InlineData("aA")] [InlineData("")]
    public void InvalidStepEditPreservesDocumentAndUndoToken(string text)
    {
        using var editor=new DesktopEditor();editor.Open(EditorTests.Record());editor.InsertStep(new(3,4));var token=editor.History!.CurrentStateToken;
        Assert.ThrowsAny<Exception>(()=>editor.EditSelectedText(text));Assert.Equal("1",editor.Selected!.Text);Assert.Equal(token,editor.History.CurrentStateToken);
    }
    [Fact] public void NextLetterSettingAndResizeSurviveSaveModelAndUndo()
    {
        using var editor=new DesktopEditor();editor.Open(EditorTests.Record());editor.SetSequenceMode(StepSequenceMode.Alphabetic);editor.SetNextSequence("az");editor.InsertStep(new(2,2));
        Assert.Equal("az",editor.Selected!.Text);Assert.Equal("ba",StepLabelFormatter.Format(editor.Document!.Sequence.GetNextValue(),StepSequenceMode.Alphabetic));
        editor.Resize(new(240,120));using var png=SkiaDocumentRenderer.Decode(editor.RenderPng());Assert.Equal(240,png.Width);Assert.Equal(120,png.Height);
        Assert.Equal(new PixelSize(240,120),editor.Snapshot().ResizeSize);editor.History!.Undo();Assert.Null(editor.Document.ResizeSize);
    }
    [Fact] public void ToolSpecificEditSelectsExistingShapeRatherThanAddingAnother()
    {
        using var editor=new DesktopEditor();editor.Open(EditorTests.Record());editor.Commit(new(){Kind=AnnotationKind.Rectangle,X=2,Y=2,Width=40,Height=30});
        var id=editor.SelectedId;editor.Tool=DesktopTool.Rectangle;var gesture=new EditorGesture(editor);gesture.Begin(new(10,10));gesture.End(new(20,20));
        Assert.Single(editor.Document!.Annotations);Assert.Equal(id,editor.SelectedId);Assert.Equal(12,editor.Selected!.X);Assert.Equal(12,editor.Selected.Y);
    }
}
