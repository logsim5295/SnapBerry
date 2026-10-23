using System.Text.Json;
using Xunit;

namespace SnapBerry.Core.Tests;

// 대문자 번호 변환·독립 카운터와 새 스타일 필드의 저장 형식 호환성을 검증한다.
public class SequenceStyleFeatureTests
{
    [Theory] [InlineData(1,"A")] [InlineData(26,"Z")] [InlineData(27,"AA")] [InlineData(52,"AZ")] [InlineData(53,"BA")] [InlineData(703,"AAA")]
    public void UppercaseLabelsRoundTrip(long value,string label)
    {Assert.Equal(label,StepLabelFormatter.Format(value,StepSequenceMode.UppercaseAlphabetic));Assert.Equal(value,StepLabelFormatter.Parse(label,StepSequenceMode.UppercaseAlphabetic));}

    [Theory] [InlineData("a")] [InlineData("Ab")] [InlineData("A1")] [InlineData("")]
    public void UppercaseLabelsRejectInvalidInput(string value)=>Assert.Throws<FormatException>(()=>StepLabelFormatter.Parse(value,StepSequenceMode.UppercaseAlphabetic));

    [Fact] public void ThreeCountersAdvanceIndependentlyAndUndoRestoresUppercase()
    {
        var doc=new AnnotationDocument(100,100);var history=new CommandHistory(doc);
        history.SetSequence(new(){Mode=StepSequenceMode.UppercaseAlphabetic,NextNumeric=4,NextAlphabetic=5,NextUppercaseAlphabetic=26});
        history.InsertStep(new(0,0),new());Assert.Equal("Z",doc.Annotations.Single().Text);Assert.Equal(27,doc.Sequence.NextUppercaseAlphabetic);
        history.Undo();Assert.Equal(26,doc.Sequence.NextUppercaseAlphabetic);history.Redo();
        history.SetSequence(doc.Sequence with{Mode=StepSequenceMode.Alphabetic});history.InsertStep(new(0,0),new());
        history.SetSequence(doc.Sequence with{Mode=StepSequenceMode.Numeric});history.InsertStep(new(0,0),new());
        history.SetSequence(doc.Sequence with{Mode=StepSequenceMode.UppercaseAlphabetic});history.InsertStep(new(0,0),new());
        Assert.Equal(new[]{"Z","e","4","AA"},doc.Annotations.Select(x=>x.Text));
        Assert.Equal((5L,6L,28L),(doc.Sequence.NextNumeric,doc.Sequence.NextAlphabetic,doc.Sequence.NextUppercaseAlphabetic));
        Assert.Throws<ArgumentOutOfRangeException>(()=>history.SetSequence(doc.Sequence with{NextUppercaseAlphabetic=0}));
    }

    [Fact] public void LegacyMetadataDefaultsToSolidAndUppercaseStartsAtA()
    {
        var sequence=JsonSerializer.Deserialize<StepSequenceState>("{\"Mode\":1,\"NextNumeric\":3,\"NextAlphabetic\":4}")!;
        Assert.Equal(1,sequence.NextUppercaseAlphabetic);Assert.Equal(StepSequenceMode.Alphabetic,sequence.Mode);
        Assert.Equal(StrokePattern.Solid,JsonSerializer.Deserialize<AnnotationStyle>("{}")!.LineStyle);
    }
}
