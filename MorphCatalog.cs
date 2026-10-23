using System.Globalization;
using System.Text;

namespace MMDDeskPet;

/// <summary>
/// Classifies PMX morph names for the desktop-pet menus.
/// Standard MMD Japanese names are matched exactly, and the English names used
/// by the original bundled models are kept so those models still sort into the
/// eye and mouth menus.
/// </summary>
internal static class MorphCatalog
{
    private static readonly HashSet<string> EyeKeys = BuildKeys(
        // Standard MMD eye / brow morphs.
        "まばたき",
        "まばたき左",
        "まばたき右",
        "瞬き",
        "笑い",
        "笑い左",
        "笑い右",
        "ウィンク",
        "ウィンク左",
        "ウィンク右",
        "ウィンク２",
        "ウィンク2",
        "ｳｨﾝｸ２右",
        "ウィンク２右",
        "なごみ",
        "なごみ左",
        "なごみ右",
        "はぅ",
        "はう",
        "はぅ左",
        "はぅ右",
        "びっくり",
        "びっくり２",
        "びっくり2",
        "じと目",
        "ジト目",
        "じと目左",
        "じと目右",
        "ｷﾘｯ",
        "キリッ",
        "なぬ！",
        "なぬ!",
        "なぬ",
        "瞳小",
        "瞳大",
        "瞳孔小",
        "見開き",
        "真面目",
        "困る",
        "困り",
        "困った",
        "怒り",
        "怒り目",
        "悲しい",
        "悲しむ",
        "上",
        "下",
        "星目",
        "ハート",
        "ハート目",
        "ハイライト消し",
        "ハイライトなし",
        "ぐるぐる目",
        "ぐるぐる",
        "眨眼",
        "眨眼左",
        "眨眼右",
        // English names from the original bundled models.
        "Blink",
        "Blink_L",
        "Blink_R",
        "Laugh",
        "Laugh_L",
        "Laugh_R",
        "Fleer",
        "Relax",
        "Jitome",
        "EyeAngry",
        "Surprised",
        "SadSquint",
        "Squint",
        "SquintLow",
        "Puzzled",
        "UpperLid",
        "LookUP",
        "LookDOWN",
        "LookNEAR",
        "LookAT",
        "Iris-",
        "Pupil-",
        "BlackEye",
        "PupilHeart",
        "StarEyes",
        "x_HiLit",
        "x_Pupil",
        "Serious",
        "Anger",
        "BrowMid+",
        "BrowMid-",
        "BrowIn+",
        "BrowIn-",
        "BrowV+",
        "BrowV-");

    private static readonly HashSet<string> MouthKeys = BuildKeys(
        // Standard MMD mouth morphs. Short names are exact matches only.
        "あ",
        "い",
        "う",
        "え",
        "お",
        "あ２",
        "い２",
        "う２",
        "え２",
        "お２",
        "あ2",
        "い2",
        "う2",
        "え2",
        "お2",
        "ん",
        "ん２",
        "ん2",
        "ワ",
        "わ",
        "にやり",
        "にやり２",
        "にやり2",
        "にやり左",
        "にやり右",
        "にっこり",
        "にっこり右",
        "にこ",
        "∧",
        "▲",
        "∨",
        "□",
        "ω",
        "ω□",
        "口角上げ",
        "口角下げ",
        "口横広げ",
        "口角上げ左",
        "口角上げ右",
        "口角下げ左",
        "口角下げ右",
        "歯無し上",
        "歯無し下",
        "てへぺろ",
        "ぺろっ",
        "ぺろ",
        "べー",
        "べーっ",
        "あは",
        "おほほ",
        "はんっ！",
        "はんっ",
        "あはん",
        "えー",
        "えー２",
        "えー2",
        "口大",
        "口小",
        // English names from the original bundled models.
        "Aa",
        "Ih",
        "Ou",
        "Ee",
        "Oh",
        "Wa",
        "Whee",
        "Shout",
        "Nya",
        "Sniff",
        "LipMid+",
        "LipMid-",
        "CornerUP",
        "GrinClose",
        "Grin",
        "Tricky",
        "CornerDOWN",
        "Bad_L",
        "Annoy_L",
        "AnnoyLow_L",
        "PuffCheek_L",
        "CornerWIDE",
        "FoolSmile");

    internal static bool IsEyeExpression(string name, string englishName)
    {
        return Matches(name, EyeKeys) || Matches(englishName, EyeKeys);
    }

    internal static bool IsMouthExpression(string name, string englishName)
    {
        return Matches(name, MouthKeys) || Matches(englishName, MouthKeys);
    }

    private static bool Matches(string value, HashSet<string> keys)
    {
        var key = Normalize(value);
        return key.Length > 0 && keys.Contains(key);
    }

    private static string Normalize(string value)
    {
        return value
            .Trim()
            .Normalize(NormalizationForm.FormKC)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("　", string.Empty, StringComparison.Ordinal)
            .ToLower(CultureInfo.InvariantCulture);
    }

    private static HashSet<string> BuildKeys(params string[] names)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            var key = Normalize(name);
            if (key.Length > 0)
            {
                keys.Add(key);
            }
        }

        return keys;
    }
}
