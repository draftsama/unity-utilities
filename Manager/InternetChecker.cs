using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class InternetChecker : MonoBehaviour
{
   public static InternetChecker Instance
   {
      get
      {
         if (_Instance == null)
         {
            GameObject go = new GameObject("InternetChecker",typeof(InternetChecker));
            _Instance = go.GetComponent<InternetChecker>();
         }
         return _Instance;
      }
   }
   private static InternetChecker _Instance;
   

   public void Check(Action<bool> _callback,int _timeOut = 3000)
   {
      StartCoroutine(StartCheckInternet(_callback,_timeOut));
   }

   IEnumerator StartCheckInternet(Action<bool> _callback,int _timeOut = 3000)
   {
      UnityWebRequest request = UnityWebRequest.Get("https://www.google.com");
     request.timeout = Mathf.RoundToInt(_timeOut/1000f);
     yield return request.SendWebRequest();
     _callback.Invoke(!request.isNetworkError);
   }
  
  
}
