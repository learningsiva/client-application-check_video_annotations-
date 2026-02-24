using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;



public class NextScreen : MonoBehaviour
{
    public string scname;
   
    // Update is called once per frame
    
   public  void nextscreen()
    {
        SceneManager.LoadScene(scname);
    }
}
