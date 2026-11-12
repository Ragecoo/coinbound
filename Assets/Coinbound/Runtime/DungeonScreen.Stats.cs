using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Coinbound
{
    public sealed partial class DungeonScreen
    {
        private RectTransform statsPanel;
        private readonly Text[] statValues = new Text[6];
        private readonly Text[] statSigns = new Text[6];

        private void BuildStatsHud()
        {
            RectTransform panel = Rect("Player stats", hud.transform, Vector2.zero, new Vector2(110, 160));
            statsPanel = panel;
            Anchor(panel, new Vector2(0, 1), new Vector2(24, -96));
            string[] icons = { "damage", "movement", "attack-rate", "critical", "luck", "block" };
            for (int i = 0; i < icons.Length; i++)
            {
                float y = 64 - i * 32;
                HudIcon("Stat icon " + icons[i], panel, icons[i], new Vector2(-45, y), 32);
                statValues[i] = Label("Stat value " + icons[i], panel, "", 12,
                    new Vector2(11, y + 2), new Vector2(70, 20), Pale);
                statValues[i].fontSize = 16;
                ((DungeonPixelText)statValues[i]).ExtraPixelSteps = 0;
                statValues[i].horizontalOverflow = HorizontalWrapMode.Overflow;
                statValues[i].alignment = TextAnchor.MiddleLeft;
                statValues[i].gameObject.AddComponent<Shadow>().effectColor = Color.black;
                // A separate sign column keeps the first digit fixed when a stat becomes negative.
                statSigns[i] = Label("Stat sign " + icons[i], panel, "", 12,
                    new Vector2(-30, y + 2), new Vector2(12, 20), Pale);
                statSigns[i].fontSize = 16;
                ((DungeonPixelText)statSigns[i]).ExtraPixelSteps = 0;
                statSigns[i].horizontalOverflow = HorizontalWrapMode.Overflow;
                statSigns[i].alignment = TextAnchor.MiddleRight;
                statSigns[i].gameObject.AddComponent<Shadow>().effectColor = Color.black;
            }
        }

        private void RefreshStats(PlayerStats stats)
        {
            string Number(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);
            string[] values = { Number(stats.Damage),
                Number(stats.MoveSpeed), Number(stats.AttackRate), Number(stats.CriticalRate),
                Number(stats.Luck), Number(stats.Defense) };
            for (int i = 0; i < values.Length; i++)
            {
                bool negative = values[i].StartsWith("-", System.StringComparison.Ordinal);
                statSigns[i].text = negative ? "-" : "";
                statValues[i].text = negative ? values[i].Substring(1) : values[i];
            }
        }
    }
}
