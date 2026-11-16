using UnityEngine;

namespace YuDian
{
    /// <summary>车站界面共用的旧金属牌与票据边框。</summary>
    internal static class YudianHud
    {
        internal static readonly Color Ink = new Color(.028f, .048f, .047f, .94f);
        internal static readonly Color Brass = new Color(.86f, .65f, .32f, 1f);
        internal static readonly Color Muted = new Color(.55f, .66f, .62f, 1f);
        internal static readonly Color Line = new Color(.48f, .54f, .44f, .42f);
        internal static void Fill(Rect rect, Color color)
        {
            Color before = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = before;
        }
        internal static void Label(Rect rect, string text, GUIStyle style, Color color)
        { style.normal.textColor = color; GUI.Label(rect, text, style); }
        internal static void Frame(Rect r, Color accent)
        {
            Fill(r, Ink);
            Fill(new Rect(r.x, r.y, 3, r.height), accent);
            Fill(new Rect(r.x, r.y, r.width, 1), Line);
            Fill(new Rect(r.x, r.yMax - 1, r.width, 1), Line);
            Fill(new Rect(r.xMax - 15, r.y + 4, 9, 2), accent);
            Fill(new Rect(r.xMax - 7, r.y + 4, 2, 9), accent);
        }
    }
}
