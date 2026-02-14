using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChooseWhat : MonoBehaviour
{
    public GameObject MiniWindow;

    private bool isOpen = false;

    public void ToggleWindow()
    {
        isOpen = !isOpen;
        MiniWindow.SetActive(isOpen);
    }

    // Å© ñﬂÇÈÉ{É^Éìóp
    public void CloseWindow()
    {
        isOpen = false;
        MiniWindow.SetActive(false);
    }
}
