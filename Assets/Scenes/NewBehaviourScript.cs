using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.UI;

public class NewBehaviourScript : MonoBehaviour
{
    public Text logdb;
    void GetOAID()
    {

    }

    public void onOAIDRecv(string oaid)
    {

    }

    private void Awake()
    {
    }
    // Start is called before the first frame update
    void Start()
    {
        ///logdb.text += UMConfigure == null ? "class init failed" : "fucking java is ojbk";
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void onBtn()
    {
        logdb.text = "";
        DeviceIDHelper.inst.getDeviceID((err, res) =>
        {
            if (err != null)
            {
                logdb.text = err.Message + "\r\n";
            }

            foreach(var v in res)
            {
                logdb.text += "id: " + v + "\r\n";
            }
        });
    }
}
