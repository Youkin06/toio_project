using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using toio.Samples.Sample_ConnectName;

public class mobile_button_manager : MonoBehaviour
{
    
    public void OnForwardButtonDown()
    {
        player_move_manager.instance.forwardButtonPressed = true;
    }

    public void OnForwardButtonUp()
    {
        player_move_manager.instance.forwardButtonPressed = false;
    }

    public void OnBackButtonDown()
    {
        player_move_manager.instance.backButtonPressed = true;
    }

    public void OnBackButtonUp()
    {
        player_move_manager.instance.backButtonPressed = false;
    }

    public void OnLeftButtonDown()
    {
        player_move_manager.instance.leftButtonPressed = true;
    }

    public void OnLeftButtonUp()
    {
        player_move_manager.instance.leftButtonPressed = false;
    }

    public void OnRightButtonDown()
    {
        player_move_manager.instance.rightButtonPressed = true;
    }

    public void OnRightButtonUp()
    {
        player_move_manager.instance.rightButtonPressed = false;
    }
}
