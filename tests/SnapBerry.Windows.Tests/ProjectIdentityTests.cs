using SnapBerry.App.Editor;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 프로젝트·어셈블리·네임스페이스가 SnapBerry 이름으로 일치하는지 검증한다.
public class ProjectIdentityTests
{
    [Fact]
    public void ProjectsAndNamespacesUseSnapBerry()
    {
        Assert.Equal("SnapBerry.App.Editor",typeof(EditorWindow).Namespace);
        Assert.Equal("SnapBerry.Core",typeof(Annotation).Namespace);
        Assert.Equal("SnapBerry.Core",typeof(Annotation).Assembly.GetName().Name);
        Assert.Equal("SnapBerry.Windows.Tests",typeof(ProjectIdentityTests).Namespace);
        Assert.Equal("SnapBerry.Windows.Tests",typeof(ProjectIdentityTests).Assembly.GetName().Name);
        Assert.Equal("SnapBerry",typeof(EditorWindow).Assembly.GetName().Name);
    }
}
