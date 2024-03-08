using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThirdPersonAim : MonoBehaviour
{
    
    public CharacterController characterController;    
    private Ray aim;

    void Start()
    {
        
    }

    void Update()
    {
        if (Input.GetMouseButton(0) /*Left mouse click*/) { RayCast(); }        
    }
    
    private void RayCast()
    {
        //Middle of screen
        aim = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));

        if (Physics.Raycast(aim, out var hit, 150f))
        {            
            Vector3 cameraHitPoint = hit.point;
            //transform.position will change to gun.position
            Physics.Raycast(transform.position + new Vector3(0f, characterController.height - characterController.center.y, 0f ), cameraHitPoint - transform.position - new Vector3(0f, characterController.height - characterController.center.y, 0f ), out hit, 150f);
            Debug.DrawRay(transform.position + new Vector3(0f, characterController.height - characterController.center.y, 0f ), cameraHitPoint - transform.position - new Vector3(0f, characterController.height - characterController.center.y, 0f ), Color.red);
        }
    }
}
