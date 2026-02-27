using UnityEngine;
using UnityEngine.EventSystems;

public class WeaponSlotClickHandler : MonoBehaviour, IPointerClickHandler
{
    private WeaponSlotUI owner;
    private int slotIndex;

    public void Init(WeaponSlotUI owner, int index)
    {
        this.owner = owner;
        slotIndex = index;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner == null) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;
        owner.HandleSlotClick(slotIndex);
    }
}
