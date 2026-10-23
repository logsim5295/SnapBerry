using SnapBerry.App.Capture;
using Xunit;

namespace SnapBerry.Windows.Tests;

public class WindowTransitionScopeTests
{
    [Fact]
    public void ScopeSuppressesAllWindowsAndRestoresTransitionsOnlyOnce()
    {
        var native=new Transitions();native.Disabled[1]=false;native.Disabled[2]=false;
        var scope=new WindowTransitionScope([new(1),new(2)],native);
        Assert.True(native.Disabled[1]);Assert.True(native.Disabled[2]);
        scope.Dispose();scope.Dispose();
        Assert.False(native.Disabled[1]);Assert.False(native.Disabled[2]);
        Assert.Equal(4,native.Writes);
    }

    [Fact]
    public void FailureWhilePreparingAnotherWindowRestoresAlreadyChangedWindows()
    {
        var native=new Transitions{FailHandle=new(2)};
        native.Disabled[1]=false;native.Disabled[2]=false;
        Assert.Throws<InvalidOperationException>(()=>new WindowTransitionScope([new(1),new(2)],native));
        Assert.False(native.Disabled[1]);Assert.False(native.Disabled[2]);
    }

    [Fact]
    public void RestorationFailureStillRestoresTheOtherWindows()
    {
        var native=new Transitions();native.Disabled[1]=false;native.Disabled[2]=false;
        var scope=new WindowTransitionScope([new(1),new(2)],native);
        native.FailHandle=new(2);
        Assert.Throws<AggregateException>(scope.Dispose);
        Assert.False(native.Disabled[1]);
    }

    private sealed class Transitions : IWindowTransitions
    {
        public Dictionary<nint,bool> Disabled {get;}=[];
        public nint FailHandle {get;set;}
        public int Writes {get;private set;}
        public void SetDisabled(nint handle,bool disabled)
        {if(handle==FailHandle)throw new InvalidOperationException();Disabled[handle]=disabled;Writes++;}
    }
}
