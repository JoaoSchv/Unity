using UnityEngine;

public class JoystickDebug : MonoBehaviour
{
    void Update()
    {
        for (int i = 0; i <= 19; i++) // verifica até 20 botões
        {
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.JoystickButton0 + i)))
            {
                Debug.Log($"Botão {i} pressionado");
            }
            if (Input.GetKeyUp((KeyCode)((int)KeyCode.JoystickButton0 + i)))
            {
                Debug.Log($"Botão {i} solto");
            }
        }
    }
}