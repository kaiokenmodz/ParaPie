using System;
using System.Collections.Generic;
using UnityEngine;

namespace ParaPie
{
    /// <summary>One slice of the pie, independent of where it came from (card, interaction, category...).</summary>
    internal class Slice
    {
        public string Label = "";
        public Color Background = Color.white;
        public Color TextColor = Color.black;
        public Sprite Icon;
        public string Badge;            // e.g. "72%"
        public string Info;             // shown under the pie while hovered
        public bool Greyed;
        public bool HasChildren;        // shows a chevron, opens a sub-ring
        public bool IsBack;
        public bool HasVariants;
        public ulong Seed;              // stable per slice, drives the playful tilt
        public string Tag = "";         // "card", "category", "interaction", "back", "more" (used by the playtest)

        public Action OnClick;
        public Action OnRightClick;
        public Action<bool> OnHover;    // enter / exit
        public Action OnHoverStay;      // every frame while hovered
    }

    /// <summary>A circle of the page switcher under the pie.</summary>
    internal class Chip
    {
        public ulong Key;
        public string Name = "";
        public Sprite Portrait;
        public bool IsStoryteller;
        public bool Active;
        public Color Accent = Color.white;
        public Action OnClick;
    }

    /// <summary>Everything the view needs to draw one frame.</summary>
    internal class PieContent
    {
        public string Key = "";         // changes -> slices animate in again
        public string HubName = "";
        public Sprite HubPortrait;
        public bool HubIsStoryteller;
        public string Hint = "";
        public bool OpenAtMouse;
        public Action OnOutsideClick;   // null = clicks outside fall through to the game
        public readonly List<Slice> Slices = new List<Slice>();
        public readonly List<Chip> Chips = new List<Chip>();
    }

    internal static class Palette
    {
        public static readonly Color Cream = new Color32(255, 248, 238, 255);
        public static readonly Color Paper = new Color32(255, 253, 249, 255);
        public static readonly Color Cocoa = new Color32(84, 66, 54, 255);
        public static readonly Color SoftCocoa = new Color32(122, 102, 88, 255);
        public static readonly Color Honey = new Color32(246, 196, 94, 255);
        public static readonly Color Sand = new Color32(240, 226, 204, 255);   // regular (non-social) interactions
        public static readonly Color Shadow = new Color(0.16f, 0.10f, 0.06f, 1f);
    }
}
