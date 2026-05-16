using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Dialogue", menuName = "Dialogue System/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    [System.Serializable]
    public class DialogueLine
    {
        [TextArea(3, 5)]
        public string text;
    }

    public string npcName;
    public Sprite npcIllustration; // NPC 일러스트
    public List<DialogueLine> dialogueLines = new List<DialogueLine>();
}