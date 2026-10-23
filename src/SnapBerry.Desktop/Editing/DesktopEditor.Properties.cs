using SnapBerry.Core;

namespace SnapBerry.Desktop.Editing;

public sealed partial class DesktopEditor
{
    public void Resize(PixelSize size)=>History?.Resize(size);
    public void SetSequenceMode(StepSequenceMode mode)
    {
        if(Document is null||History is null)return;
        if(!Enum.IsDefined(mode))throw new ArgumentOutOfRangeException(nameof(mode));
        if(Document.Sequence.Mode!=mode)History.SetSequence(Document.Sequence with{Mode=mode});
    }
    public void SetNextSequence(string text)
    {
        if(Document is null||History is null)return;
        var sequence=Document.Sequence;var next=StepLabelFormatter.Parse(text.Trim(),sequence.Mode);
        if(next!=sequence.GetNextValue())History.SetSequence(sequence.WithNextValue(next));
    }
    public void EditSelectedText(string text)
    {
        if(Selected is not Annotation selected||Document is null||History is null)return;
        if(selected.Kind==AnnotationKind.Sticker){UpdateKeycapLabel(text);return;}
        AnnotationLimits.ValidateText(text);
        if(selected.Kind==AnnotationKind.Text){if(text!=selected.Text)ReplaceSelected(selected with{Text=text});return;}
        if(selected.Kind!=AnnotationKind.StepMarker)throw new InvalidOperationException("내용을 편집할 수 없는 개체입니다.");
        var label=text.Trim();var mode=long.TryParse(label,out _)?StepSequenceMode.Numeric:label.Length>0&&label.All(c=>c is >= 'A' and <= 'Z')?StepSequenceMode.UppercaseAlphabetic:StepSequenceMode.Alphabetic;
        var value=StepLabelFormatter.Parse(label,mode);var sequence=(Document.Sequence with{Mode=mode}).WithNextValue(checked(value+1));
        var updated=selected with{Text=StepLabelFormatter.Format(value,mode)};
        if(updated==selected&&sequence==Document.Sequence)return;
        var index=Document.Annotations.FindIndex(a=>a.Id==selected.Id);
        History.Execute(()=>{Document.Annotations[index]=updated;Document.Sequence=sequence;});
    }
}
