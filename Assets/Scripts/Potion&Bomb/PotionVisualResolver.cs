using System;
using UnityEngine;

public static class PotionVisualResolver
{
    private static readonly string[] DefaultFramePaths =
    {
        "PotionFrames/frame_default",
        "PotionFrames/frame",
        "PotionFrame_Default",
        "PotionFrame",
        "frame",
        "Frame"
    };

    public static PotionVisualParts Resolve(PotionData potionData)
    {
        if (potionData == null)
        {
            return PotionVisualParts.Empty;
        }

        Sprite top = potionData.topIMG != null
            ? potionData.topIMG
            : (potionData.icon != null ? potionData.icon : potionData.bottomIMG);

        Sprite bottom = potionData.bottomIMG != null
            ? potionData.bottomIMG
            : (potionData.icon != null ? potionData.icon : potionData.topIMG);

        Sprite frame = ResolveFrame(potionData);
        return new PotionVisualParts(top, bottom, frame);
    }

    public static Sprite ResolveCraftFrame(ItemData first, ItemData second, ElementType firstElement, ElementType secondElement)
    {
        string ingredientA = first != null ? first.GetIngredientId() : string.Empty;
        string ingredientB = second != null ? second.GetIngredientId() : string.Empty;

        Sprite byIngredient = TryLoadFrameByIngredients(ingredientA, ingredientB);
        if (byIngredient != null)
        {
            return byIngredient;
        }

        Sprite byElement = TryLoadFrameByElements(firstElement, secondElement);
        if (byElement != null)
        {
            return byElement;
        }

        return GetDefaultFrame();
    }

    private static Sprite ResolveFrame(PotionData potionData)
    {
        if (potionData == null)
        {
            return null;
        }

        if (potionData.frameIMG != null)
        {
            return potionData.frameIMG;
        }

        PotionPhaseSpec phase1 = potionData.GetPhase(0);
        PotionPhaseSpec phase2 = potionData.GetPhase(1);

        string ingredientA = phase1 != null ? phase1.ingredientId : string.Empty;
        string ingredientB = phase2 != null ? phase2.ingredientId : string.Empty;

        Sprite byIngredient = TryLoadFrameByIngredients(ingredientA, ingredientB);
        if (byIngredient != null)
        {
            return byIngredient;
        }

        ElementType firstElement = phase1 != null ? phase1.primaryElement : ToElementType(potionData.element1);
        ElementType secondElement = phase2 != null ? phase2.primaryElement : ToElementType(potionData.element2);

        Sprite byElement = TryLoadFrameByElements(firstElement, secondElement);
        if (byElement != null)
        {
            return byElement;
        }

        return potionData.icon != null ? potionData.icon : GetDefaultFrame();
    }

    private static Sprite TryLoadFrameByIngredients(string ingredientA, string ingredientB)
    {
        string normalizedA = NormalizeKey(ingredientA);
        string normalizedB = NormalizeKey(ingredientB);
        if (string.IsNullOrEmpty(normalizedA) || string.IsNullOrEmpty(normalizedB))
        {
            return null;
        }

        string[] paths =
        {
            $"PotionFrames/{normalizedA}_{normalizedB}_frame",
            $"PotionFrames/{normalizedA}_{normalizedB}",
            $"PotionFrames/{normalizedB}_{normalizedA}_frame",
            $"PotionFrames/{normalizedB}_{normalizedA}"
        };

        return LoadFirst(paths);
    }

    private static Sprite TryLoadFrameByElements(ElementType firstElement, ElementType secondElement)
    {
        string first = NormalizeKey(firstElement.ToString());
        string second = NormalizeKey(secondElement.ToString());

        if (string.IsNullOrEmpty(first) && string.IsNullOrEmpty(second))
        {
            return null;
        }

        string[] paths =
        {
            $"PotionFrames/frame_{first}_{second}",
            $"PotionFrames/frame_{second}_{first}",
            $"PotionFrames/frame_{first}",
            $"PotionFrames/frame_{second}",
            "PotionFrames/frame_mixed"
        };

        return LoadFirst(paths);
    }

    private static Sprite GetDefaultFrame()
    {
        return LoadFirst(DefaultFramePaths);
    }

    private static Sprite LoadFirst(string[] paths)
    {
        if (paths == null)
        {
            return null;
        }

        for (int i = 0; i < paths.Length; i++)
        {
            string path = paths[i];
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
            {
                return sprite;
            }
        }

        return null;
    }

    private static string NormalizeKey(string raw)
    {
        return PotionCraftRules.NormalizeKey(raw);
    }

    private static ElementType ToElementType(Element element)
    {
        return element switch
        {
            Element.Fire => ElementType.Fire,
            Element.Lightning => ElementType.Electric,
            _ => ElementType.Water
        };
    }
}
