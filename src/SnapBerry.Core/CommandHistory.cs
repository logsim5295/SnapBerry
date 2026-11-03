namespace SnapBerry.Core;
// 주석·번호 순서·자르기 영역을 한 상태로 묶어 실행 취소와 다시 실행을 지원한다.
public sealed class CommandHistory(AnnotationDocument document)
{
    private sealed record Snapshot(Annotation[] Items,StepSequenceState Sequence,PixelRect CropBounds,PixelRect? SourceClip,PixelSize? ResizeSize,string? BackgroundAssetHash,Guid Token);
    private readonly Stack<Snapshot> undo=[];
    private readonly Stack<Snapshot> redo=[];
    private Snapshot? group;
    // 내보내기 여부와 이력 파일 저장 여부는 서로 다른 상태 토큰으로 추적한다.
    private Guid exported,persisted;
    public event EventHandler? Changed;
    public Guid CurrentStateToken {get;private set;}=Guid.NewGuid();
    public bool IsDirty=>exported!=CurrentStateToken;public bool IsPersistenceDirty=>persisted!=CurrentStateToken;
    public bool CanRedo=>redo.Count>0;public bool CanUndo=>undo.Count>0;
    private Snapshot Capture()=>new(document.Annotations.ToArray(),document.Sequence,document.CropBounds,document.SourceClip,document.ResizeSize,document.BackgroundAssetHash,CurrentStateToken);
    private void Restore(Snapshot s){document.Annotations.Clear();document.Annotations.AddRange(s.Items);document.Sequence=s.Sequence;document.CropBounds=s.CropBounds;document.SourceClip=s.SourceClip;document.ResizeSize=s.ResizeSize;document.BackgroundAssetHash=s.BackgroundAssetHash;CurrentStateToken=s.Token;Changed?.Invoke(this,EventArgs.Empty);}
    // 작업이 성공한 뒤 이전 상태를 쌓는다. 새 편집은 기존 다시 실행 경로를 무효화한다.
    public void Execute(Action action){var state=Capture();action();if(group is null){undo.Push(state);redo.Clear();}CurrentStateToken=Guid.NewGuid();Changed?.Invoke(this,EventArgs.Empty);}
    // 슬라이더의 연속 변경은 화면에 즉시 반영하되 실행 취소에는 처음과 끝만 남긴다.
    public void BeginGroup(){EndGroup();group=Capture();}
    public void EndGroup()
    {
        if(group is not Snapshot state)return;group=null;
        if(state.Items.SequenceEqual(document.Annotations)&&state.Sequence==document.Sequence&&state.CropBounds==document.CropBounds&&state.SourceClip==document.SourceClip&&state.ResizeSize==document.ResizeSize&&state.BackgroundAssetHash==document.BackgroundAssetHash)
        {if(CurrentStateToken!=state.Token){CurrentStateToken=state.Token;Changed?.Invoke(this,EventArgs.Empty);}return;}
        undo.Push(state);redo.Clear();Changed?.Invoke(this,EventArgs.Empty);
    }
    public void SetBackground(string hash)
    {
        if(!AnnotationDocument.IsAssetHash(hash))throw new ArgumentException("Invalid background image reference.",nameof(hash));
        if(document.BackgroundAssetHash==hash)return;
        EndGroup();Execute(()=>document.BackgroundAssetHash=hash);
    }
    public void Add(Annotation a,Action? prepareAsset=null)=>Execute(()=>
    {
        var bounds=ImagePlacement.CanvasFor(document.CropBounds,a);
        var resized=ResizeForCanvas(bounds);
        prepareAsset?.Invoke();
        ApplyCanvas(bounds,resized);
        document.Annotations.Add(a);
    });
    public void Remove(Guid id)=>Execute(()=>document.Annotations.RemoveAll(a=>a.Id==id));
    public void Replace(Annotation a)=>Execute(()=>
    {
        var i=document.Annotations.FindIndex(x=>x.Id==a.Id);
        if(i<0)throw new InvalidOperationException();
        var previous=document.Annotations[i];
        if((previous.X,previous.Y,previous.Width,previous.Height)!=(a.X,a.Y,a.Width,a.Height))
        {
            var bounds=ImagePlacement.CanvasFor(document.CropBounds,a);
            ApplyCanvas(bounds,ResizeForCanvas(bounds));
        }
        document.Annotations[i]=a;
    });
    public void Crop(PixelRect relativeBounds)
    {
        // 현재 보이는 이미지 기준 영역을 원본 기준으로 환산하므로 연속 자르기도 복원 가능하다.
        if(!CropPlacement.IsValidCanvas(relativeBounds)
            ||relativeBounds.Intersect(new(0,0,document.Width,document.Height)).Width==0
            ||(long)relativeBounds.X+document.CropBounds.X is <=int.MinValue or >int.MaxValue
            ||(long)relativeBounds.Y+document.CropBounds.Y is <=int.MinValue or >int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(relativeBounds));
        var absolute=relativeBounds.Translate(document.CropBounds.X,document.CropBounds.Y);
        if(!CropPlacement.IsValidCanvas(absolute))throw new ArgumentOutOfRangeException(nameof(relativeBounds));
        if(absolute!=document.CropBounds)Execute(()=>ApplyCanvas(absolute,ResizeForCanvas(absolute)));
    }
    private PixelSize? ResizeForCanvas(PixelRect bounds)
    {
        if(document.ResizeSize is not PixelSize size||bounds==document.CropBounds)return document.ResizeSize;
        var width=Math.Round((double)bounds.Width*size.Width/document.Width);
        var height=Math.Round((double)bounds.Height*size.Height/document.Height);
        var resized=new PixelSize((int)Math.Clamp(width,1,int.MaxValue),(int)Math.Clamp(height,1,int.MaxValue));
        ImageResize.Validate(resized);
        return resized;
    }
    private void ApplyCanvas(PixelRect bounds,PixelSize? resized)
    {
        if(bounds==document.CropBounds)return;
        if(bounds.Intersect(document.CropBounds)!=bounds)
        {
            var visible=document.VisibleSource;
            document.SourceClip=visible==new PixelRect(0,0,document.SourceWidth,document.SourceHeight)?null:visible;
            for(var i=0;i<document.Annotations.Count;i++)
            {
                var a=document.Annotations[i];
                if(a.Kind!=AnnotationKind.Mosaic)
                    document.Annotations[i]=a with{CropClip=AnnotationClip.FromCanvas(a,document.CropBounds)};
            }
        }
        document.CropBounds=bounds;
        document.ResizeSize=resized;
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
