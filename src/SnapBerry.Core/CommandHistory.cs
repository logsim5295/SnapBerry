namespace SnapBerry.Core;
// 주석·번호 순서·자르기 영역을 한 상태로 묶어 실행 취소와 다시 실행을 지원한다.
public sealed class CommandHistory(AnnotationDocument document)
{
    private sealed record Snapshot(Annotation[] Items,StepSequenceState Sequence,PixelRect CropBounds,PixelSize? ResizeSize,Guid Token);
    private readonly Stack<Snapshot> undo=[];
    private readonly Stack<Snapshot> redo=[];
    private Snapshot? group;
    // 내보내기 여부와 이력 파일 저장 여부는 서로 다른 상태 토큰으로 추적한다.
    private Guid exported,persisted;
    public event EventHandler? Changed;
    public Guid CurrentStateToken {get;private set;}=Guid.NewGuid();
    public bool IsDirty=>exported!=CurrentStateToken;public bool IsPersistenceDirty=>persisted!=CurrentStateToken;
    public bool CanRedo=>redo.Count>0;public bool CanUndo=>undo.Count>0;
    private Snapshot Capture()=>new(document.Annotations.ToArray(),document.Sequence,document.CropBounds,document.ResizeSize,CurrentStateToken);
    private void Restore(Snapshot s){document.Annotations.Clear();document.Annotations.AddRange(s.Items);document.Sequence=s.Sequence;document.CropBounds=s.CropBounds;document.ResizeSize=s.ResizeSize;CurrentStateToken=s.Token;Changed?.Invoke(this,EventArgs.Empty);}
    // 작업이 성공한 뒤 이전 상태를 쌓는다. 새 편집은 기존 다시 실행 경로를 무효화한다.
    public void Execute(Action action){var state=Capture();action();if(group is null){undo.Push(state);redo.Clear();}CurrentStateToken=Guid.NewGuid();Changed?.Invoke(this,EventArgs.Empty);}
    // 슬라이더의 연속 변경은 화면에 즉시 반영하되 실행 취소에는 처음과 끝만 남긴다.
    public void BeginGroup(){EndGroup();group=Capture();}
    public void EndGroup()
    {
        if(group is not Snapshot state)return;group=null;
        if(state.Items.SequenceEqual(document.Annotations)&&state.Sequence==document.Sequence&&state.CropBounds==document.CropBounds&&state.ResizeSize==document.ResizeSize)
        {if(CurrentStateToken!=state.Token){CurrentStateToken=state.Token;Changed?.Invoke(this,EventArgs.Empty);}return;}
        undo.Push(state);redo.Clear();Changed?.Invoke(this,EventArgs.Empty);
    }
    public void Add(Annotation a)=>Execute(()=>document.Annotations.Add(a));
    public void Remove(Guid id)=>Execute(()=>document.Annotations.RemoveAll(a=>a.Id==id));
    public void Replace(Annotation a)=>Execute(()=>{var i=document.Annotations.FindIndex(x=>x.Id==a.Id);if(i<0)throw new InvalidOperationException();document.Annotations[i]=a;});
    public void Crop(PixelRect relativeBounds)
    {
        // 현재 보이는 이미지 기준 영역을 원본 기준으로 환산하므로 연속 자르기도 복원 가능하다.
        if(!CropPlacement.IsWithin(relativeBounds,document.Width,document.Height))throw new ArgumentOutOfRangeException(nameof(relativeBounds));
        var absolute=relativeBounds.Translate(document.CropBounds.X,document.CropBounds.Y);
        if(absolute!=document.CropBounds)
        {
            PixelSize? resized=document.ResizeSize is PixelSize size?new(Math.Max(1,(int)Math.Round((double)relativeBounds.Width*size.Width/document.Width)),Math.Max(1,(int)Math.Round((double)relativeBounds.Height*size.Height/document.Height))):null;
            Execute(()=>{document.CropBounds=absolute;document.ResizeSize=resized;});
        }
    }
    public void InsertStep(PixelPoint point,AnnotationStyle style)
    {
        // 번호 생성과 다음 순번 증가는 한 번의 실행 취소로 함께 복원한다.
        var s=document.Sequence;var value=s.GetNextValue();
        var next=checked(value+1);var label=StepLabelFormatter.Format(value,s.Mode);
        Execute(()=>{document.Annotations.Add(new(){Kind=AnnotationKind.StepMarker,X=point.X,Y=point.Y,Width=48,Height=48,Text=label,Style=style});document.Sequence=s.WithNextValue(next);});
    }
    public void SetSequence(StepSequenceState state)
    {if(state.NextNumeric<1||state.NextAlphabetic<1||state.NextUppercaseAlphabetic<1||!Enum.IsDefined(state.Mode))throw new ArgumentOutOfRangeException(nameof(state));Execute(()=>document.Sequence=state);}
    public void Undo(){EndGroup();if(!CanUndo)return;redo.Push(Capture());Restore(undo.Pop());}
    public void Redo(){EndGroup();if(!CanRedo)return;undo.Push(Capture());Restore(redo.Pop());}
    public void MarkExported(Guid token)=>exported=token;public void MarkPersisted(Guid token)=>persisted=token;
    public void Resize(PixelSize size)
    {
        ImageResize.Validate(size);if(size==document.OutputSize)return;
        Execute(()=>document.ResizeSize=size==new PixelSize(document.Width,document.Height)?null:size);
    }
}
