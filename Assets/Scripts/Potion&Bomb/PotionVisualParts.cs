using UnityEngine;

public readonly struct PotionVisualParts
{
    public static PotionVisualParts Empty => new(null, null, null);

    public Sprite Top { get; }
    public Sprite Bottom { get; }
    public Sprite Frame { get; }

    public bool HasAny => Top != null || Bottom != null || Frame != null;

    public PotionVisualParts(Sprite top, Sprite bottom, Sprite frame)
    {
        Top = top;
        Bottom = bottom;
        Frame = frame;
    }
}
