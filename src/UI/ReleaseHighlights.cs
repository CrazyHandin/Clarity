namespace ClearSpace {
public static class ReleaseHighlights {
 public const string Chinese=ReleaseVersion.HighlightsChinese;
 public const string English=ReleaseVersion.HighlightsEnglish;
 public static string Current{get{return UiText.English?English:Chinese;}}
}
}
