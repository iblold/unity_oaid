最近一个项目要对接一个sdk, 里面要求传入imei, 没有imei的传入oaid。看了一下oaid的官网，无论下载还是看文档都要注册等审核， 等不了。 直接从别的地方下载了oaid的sdk开始弄。可惜网上没有太多的可以参考的东西， 有一篇写的很详细的在unity中使用oaid的文章， 但是操作方法太复杂了， 因为用的是unity5所以对java的调用极其繁琐。用了小半天时间弄好了， 现在记录一下方法。

1、下载oaid sdk, 解压，里面有用的是一个aar文件，一个json文件

2 、在unity Assets中创建Plugins\Android目录， 把aar文件拷贝到这个目录。再在Plugins\Android目录下创建一个assets目录， 把json文件拷贝进去。

3、在Plugins\Android目录下创建一个java文件，我的文件如下：

package com.unity.androidplugin;

import android.content.Context;
import android.util.Log;

import com.bun.miitmdid.core.MdidSdkHelper;
import com.bun.miitmdid.interfaces.IIdentifierListener;
import com.bun.miitmdid.interfaces.IdSupplier;
import com.unity3d.player.UnityPlayer;

public class OAIDHelper implements IIdentifierListener {

    String cbGameObject = "";
    String cbFunc = "";

    public static OAIDHelper _inst = null;

    public OAIDHelper(){
        _inst = this;
    }

    public static OAIDHelper inst(){
        if (_inst == null){
            _inst = new OAIDHelper();
        }
        return _inst;
    }

    public int GetDeviceID(Context cxt, String gameObject, String cbFunc){
        cbGameObject = gameObject;
        this.cbFunc = cbFunc;

        int r = MdidSdkHelper.InitSdk(cxt, true, this);

        Log.i("GetDeviceID", "obj: " + cbGameObject + " cb:" + cbFunc + " init:" + r);

        return r;
    }

    /** 获取id回调 */
    @Override
    public void OnSupport(boolean b, IdSupplier idSupplier) {
        if(idSupplier != null) {
            String oaid = idSupplier.getOAID();
            if(!cbGameObject.isEmpty() && !cbFunc.isEmpty()){
                UnityPlayer.UnitySendMessage(cbGameObject, cbFunc, oaid == null ? "" : oaid);
            }
            Log.i("GetDeviceID", "OnSupport: " + b + " id: " + (oaid == null ? "" : oaid));
        } else {
            Log.i("GetDeviceID", "OnSupport IdSupplier null");
            if(!cbGameObject.isEmpty() && !cbFunc.isEmpty()){
                UnityPlayer.UnitySendMessage(cbGameObject, cbFunc, "");
            }
        }
    }

}
其中要注意两点， 包名是package com.unity.androidplugin，类必须继承自IIdentifierListener。使用时直接调用MdidSdkHelper.InitSdk， 看返回结果， 目前只有结果为ErrorCode.INIT_ERROR_RESULT_DELAY才是执行成功了， 此时等待OnSupport调用，用idSupplier取出oaid。

4、C#部分代码如下：

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Android;

public class DeviceIDHelperObject : MonoBehaviour
{
    Action<string> cbFunc = null;
    float timer = 0;
    bool waiting = false;

    Func<bool> condition = null;
    Action<bool> runFunc = null;
    float tiemrWaitFor = 0;

    public void setCbFunc(Action<string> cbFunc)
    {
        this.cbFunc = cbFunc;
    }

    public void waitResult(float timeout = 5)
    {
        timer = timeout;
        waiting = true;
    }

    public void onOAIDRecv(string oaid)
    {
        if (cbFunc != null && waiting)
        {
            cbFunc(oaid);
            waiting = false;
            timer = 0;
        }
    }

    public void waitFor(Func<bool> c, Action<bool> f, float timeout = 30)
    {
        condition = c;
        runFunc = f;
        tiemrWaitFor = timeout;
    }

    private void Update()
    {
        if (timer > 0)
        {
            timer -= Time.deltaTime;
            if (timer <= 0)
            {
                waiting = false;
                timer = 0;
                if (cbFunc != null)
                {
                    cbFunc("");
                }
            }
        }

        if ( condition != null && runFunc != null)
        {
            if (condition())
            {
                runFunc(true);
                condition = null;
                runFunc = null;
                tiemrWaitFor = 0;
            }
            if (tiemrWaitFor > 0)
            {
                tiemrWaitFor -= Time.deltaTime;
                if (tiemrWaitFor <= 0)
                {
                    runFunc(false);
                    condition = null;
                    runFunc = null;
                    tiemrWaitFor = 0;
                }
            }
        }
    }
}

/**
 * oaid sdk定义的错误内容
 */
public enum OAIDSdkErrorCode
{
    INIT_ERROR_BEGIN = 1008610,

    INIT_ERROR_MANUFACTURER_NOSUPPORT = 1008611,

    INIT_ERROR_DEVICE_NOSUPPORT = 1008612,

    INIT_ERROR_LOAD_CONFIGFILE = 1008613,

    INIT_ERROR_RESULT_DELAY = 1008614,

    INIT_HELPER_CALL_ERROR = 1008615,
}

public class DeviceIDHelper
{
    static string helperName = "DeviceHelper_Instance_Dont_Delete";
    static DeviceIDHelper _inst = null;
    public static DeviceIDHelper inst
    {
        get
        {
            if (_inst == null)
            {
                _inst = new DeviceIDHelper();
            }
            return _inst;
        }
    }

    Action<Exception, string[]> cbFunc = null;
    DeviceIDHelperObject helper = null;

    public DeviceIDHelper()
    {
        var obj = GameObject.Find(helperName);
        if (null == obj)
        {
            helper = new GameObject(helperName).AddComponent<DeviceIDHelperObject>();
            helper.setCbFunc((string oaid) => {
                if (cbFunc != null)
                {
                    if (oaid != null && oaid.Length > 0)
                    {
                        cbFunc(null, new string[] { oaid });
                    }
                    else
                    {
                        cbFunc(new Exception("unknow error."), new string[] { SystemInfo.deviceUniqueIdentifier });
                    }
                }
            });
        } 
        else
        {
            helper = obj.GetComponent<DeviceIDHelperObject>();
        }
    }

    int GetSdkLevel()
    {
#if UNITY_ANDROID
        var buildVersionClass = AndroidJNI.FindClass("android/os/Build$VERSION");
        var sdkIntField = AndroidJNI.GetStaticFieldID(buildVersionClass, "SDK_INT", "I");

        return AndroidJNI.GetStaticIntField(buildVersionClass, sdkIntField);
#endif
        return 0;
    }

    /**
     * 获取设备id, 如果有imei则返回imei
     * 或者返回OAID
     * 如果都获取不到, 返回错误和SystemInfo.deviceUniqueIdentifier
     */
    public void getDeviceID(Action<Exception, string[]> cbFunc, bool onlyOAID = false)
    {
#if UNITY_ANDROID
        if (onlyOAID)
        {
            realGet(cbFunc, onlyOAID);
        }
        else
        {
            Permission.RequestUserPermission("android.permission.READ_PHONE_STATE");
            helper.waitFor(() => {
                return Permission.HasUserAuthorizedPermission("android.permission.READ_PHONE_STATE");
            }, (c) =>
            {
                if (c)
                {
                    realGet(cbFunc, onlyOAID);
                }
                else
                {
                    cbFunc(new Exception("wait permission timeout"), new string[] { SystemInfo.deviceUniqueIdentifier });
                }
            }, 30);
        }
#endif
    }

    void realGet(Action<Exception, string[]> cbFunc, bool onlyOAID = false)
    {

#if UNITY_ANDROID
        this.cbFunc = null;

        List<string> result = new List<string>();
        try
        {
            bool useOAID = true;

            var lv = GetSdkLevel();

            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var context = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                var telephoneyManager = context.Call<AndroidJavaObject>("getSystemService", "phone");

                string v1 = null, v2 = null;
                if (lv < 26 && !onlyOAID)
                {
                    v1 = telephoneyManager.Call<string>("getDeviceId", 0);
                    v2 = telephoneyManager.Call<string>("getDeviceId", 1);
                }
                else if (lv >= 26 && lv < 29 && !onlyOAID)
                {
                    v1 = telephoneyManager.Call<string>("getImei", 0);
                    v2 = telephoneyManager.Call<string>("getImei", 1);
                    if (v1 == null && v2 == null)
                    {
                        v1 = telephoneyManager.Call<string>("getMeid", 0);
                        v2 = telephoneyManager.Call<string>("getMeid", 1);
                    }
                }

                if (v1 != null && v1.Length > 0)
                {
                    result.Add(v1);
                    useOAID = false;
                }
                if (v2 != null && v2.Length > 0)
                {
                    result.Add(v2);
                    useOAID = false;
                }

                if (useOAID)
                {
                    using (var jc = new AndroidJavaClass("com.unity.androidplugin.OAIDHelper"))
                    {
                        AndroidJavaObject oaHelper = jc.CallStatic<AndroidJavaObject>("inst");

                        helper.waitResult();
                        var res = (OAIDSdkErrorCode)oaHelper.Call<int>("GetDeviceID", context, helperName, "onOAIDRecv");

                        string err = "";
                        switch (res)
                        {
                            case OAIDSdkErrorCode.INIT_ERROR_DEVICE_NOSUPPORT: //不支持的设备
                                err = "不支持的设备";
                                break;
                            case OAIDSdkErrorCode.INIT_ERROR_LOAD_CONFIGFILE: //加载配置文件出错
                                err = "加载配置文件出错";
                                break;
                            case OAIDSdkErrorCode.INIT_ERROR_MANUFACTURER_NOSUPPORT: //不支持的设备厂商
                                err = "不支持的设备厂商";
                                break;
                            //case OAIDSdkErrorCode.INIT_ERROR_RESULT_DELAY: //获取接口是异步的，结果会在回调中返回，回调执行的回调可能在工作线程
                            //    break;
                            case OAIDSdkErrorCode.INIT_HELPER_CALL_ERROR: //反射调用出错
                                err = "反射调用出错";
                                break;
                            default:
                                err = "unknow err.";
                                break;
                        }

                        if (res != OAIDSdkErrorCode.INIT_ERROR_RESULT_DELAY)
                        {
                            cbFunc(new Exception(err), new string[] { SystemInfo.deviceUniqueIdentifier });
                        }
                        else
                        {
                            this.cbFunc = cbFunc;
                        }
                    }
                }
                else
                {
                    cbFunc(null, result.ToArray());
                }
            }
        }
        catch (Exception err)
        {
            cbFunc(err, new string[] { SystemInfo.deviceUniqueIdentifier });
        }
#endif
    }

}

使用方法：

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
正常情况可以取出imei， android10以上会尝试取出oaid， 出错时返回SystemInfo.deviceUniqueIdentifier的值。