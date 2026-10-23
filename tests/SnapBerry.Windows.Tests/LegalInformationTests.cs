using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SnapBerry.App.Editor;
using Xunit;

namespace SnapBerry.Windows.Tests;

// EXE 하나에서도 약관을 오프라인으로 읽으며, 문서 열람이 외부 앱을 실행하지 않는지 확인한다.
public class LegalInformationTests
{
    [Theory]
    [InlineData(".NET","ae48df11a335dc1a615f4f938b69cba73bcf4485c4f97af49b38efb0f216353b")]
    [InlineData("Avalonia","aa0eb64b2ab2fb406928989a598a1ebe03f9d3dc42209cff7eb1ddac8c9b0024")]
    [InlineData("MicroCom.Runtime","14d6eef25c307f3c25872a16c6aef60c05bf57968c54e91287c70dd8e1ce375a")]
    [InlineData("Tmds.DBus.Protocol","46e9250fcd887cf7aa5952cd4267cf66be0c6ab2c9dec5fd4ec82f4077ebe94d")]
    public void AdditionalComponentNoticesIncludeKoreanTranslationAndExactOriginal(string component,string originalHash)
    {
        var document=ReadThirdPartyNotices();
        var korean=Regex.Match(document,@"(?ms)^### "+Regex.Escape(component)+@" — 한국어 참고 번역\n\n(.*?)(?=\n### |\n## |\z)");
        Assert.True(korean.Success,$"{component}: 한국어 번역 누락");
        Assert.Contains("모든 사본 또는 상당 부분",korean.Groups[1].Value);
        Assert.Contains("보증",korean.Groups[1].Value);
        var english=Regex.Match(document,@"(?ms)^### "+Regex.Escape(component)+@" — 영어 원문\n\n(.*?)(?=\n### |\n## |\z)");
        Assert.True(english.Success,$"{component}: 영어 원문 누락");
        Assert.Equal(originalHash,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(english.Groups[1].Value.Trim()))).ToLowerInvariant());
    }

    [Fact]
    public void DotNetRuntimeNoticesTranslateAllBundledSubnoticesAndPreserveOriginal()
    {
        var document=ReadThirdPartyNotices();
        const string koreanHeading="## .NET 런타임 하위 구성 요소 한국어 참고 번역";
        const string englishHeading="## .NET 런타임 하위 구성 요소 영어 원문";
        Assert.Contains(koreanHeading,document);
        Assert.Contains(englishHeading,document);
        var koreanStart=document.IndexOf(koreanHeading,StringComparison.Ordinal);
        var koreanEnd=document.IndexOf("## SkiaSharp 라이선스 한국어 번역",koreanStart,StringComparison.Ordinal);
        var englishStart=document.IndexOf(englishHeading,StringComparison.Ordinal)+englishHeading.Length;
        var englishEnd=document.IndexOf("## SkiaSharp license",englishStart,StringComparison.Ordinal);
        var korean=document[koreanStart..koreanEnd];
        var english=document[englishStart..englishEnd].Trim();
        var originals=Regex.Matches(english,@"(?m)^License notice for (.+)$").Select(match=>match.Groups[1].Value).ToArray();
        var translations=Regex.Matches(korean,@"(?m)^### \.NET 하위: (.+) — 한국어 참고 번역$").Select(match=>match.Groups[1].Value).ToArray();
        Assert.Equal(46,originals.Length);
        Assert.Equal(originals,translations);
        Assert.Equal("b7d4569659507a00efeea90f15a0a95bb2f8b6ce33737d86090b8e22c74a8c98",
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(english))).ToLowerInvariant());
    }

    private static string ReadThirdPartyNotices()
    {
        using var stream=typeof(AboutWindow).Assembly.GetManifestResourceStream("SnapBerry.Legal.ThirdParty")!;
        using var reader=new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n","\n");
    }

    [Fact]
    public void ThirdPartyNoticesTranslateEveryComponentAndPreserveEnglishOriginals()
    {
        var document=ReadThirdPartyNotices();
        const string translationHeading="## 개별 라이선스 한국어 참고 번역";
        const string originalHeading="## 원문 라이선스 및 저작권 고지";
        Assert.Contains(translationHeading,document);
        var translationStart=document.IndexOf(translationHeading,StringComparison.Ordinal);
        var originalStart=document.IndexOf(originalHeading,StringComparison.Ordinal);
        Assert.True(originalStart>translationStart);
        var translations=document[translationStart..originalStart];
        var originals=document[originalStart..];
        var originalComponents=Regex.Matches(originals,@"(?m)^# (?!END:|https:)([^#\n]+)$")
            .Select(match=>match.Groups[1].Value).ToArray();
        var translatedComponents=Regex.Matches(translations,@"(?m)^### (.+) — 한국어 참고 번역$")
            .Select(match=>match.Groups[1].Value).ToArray();
        Assert.Equal(21,originalComponents.Length);
        Assert.Equal(originalComponents,translatedComponents);
        // 참고 번역을 추가해도 공급자 라이선스 원문은 한 글자도 바꾸지 않는다.
        var english=originals[originals.IndexOf("## SkiaSharp license",StringComparison.Ordinal)..].TrimEnd();
        Assert.Equal("fad7883eff12b096627cbb5cc08eeba2c9056f2a14cc95b045506bbbebfe5716",
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(english))).ToLowerInvariant());
    }

    [Fact]
    public void AboutContainsCompleteOfflineDocuments()=>Sta.Run(()=>
    {
        var externalActions=0;
        var window=new AboutWindow(copyText:_=>externalActions++,openLocation:_=>externalActions++);
        try
        {
            var section=Assert.IsType<Expander>(window.FindName("AboutLegalInformation"));
            Assert.False(section.IsExpanded);
            section.IsExpanded=true;
            var tabs=Assert.IsType<TabControl>(section.Content);
            Assert.Equal(3,tabs.Items.Count);
            foreach(var item in tabs.Items.Cast<TabItem>())
            {
                tabs.SelectedItem=item;
                var text=Assert.IsType<TextBox>(item.Content);
                Assert.True(text.IsReadOnly);
                Assert.True(text.AcceptsReturn);
                Assert.Equal(TextWrapping.Wrap,text.TextWrapping);
                Assert.Equal(ScrollBarVisibility.Auto,text.VerticalScrollBarVisibility);
                Assert.False(string.IsNullOrWhiteSpace(text.Text));
            }
            var terms=Assert.IsType<TextBox>(((TabItem)tabs.Items[0]).Content).Text;
            Assert.Contains("개인 및 기업",terms);
            Assert.Contains("소스 코드",terms);
            Assert.Contains("고의 또는 중대한 과실",terms);
            var data=Assert.IsType<TextBox>(((TabItem)tabs.Items[1]).Content).Text;
            Assert.Contains("%LOCALAPPDATA%\\SnapBerry",data);
            Assert.Contains("원본",data);
            Assert.Contains("자동 전송하지 않습니다",data);
            Assert.Contains("예외",data);
            Assert.Contains("클립보드",data);
            Assert.Equal(0,externalActions);
            Assert.False(window.IsVisible);
        }
        finally{window.Close();}
    });
}
