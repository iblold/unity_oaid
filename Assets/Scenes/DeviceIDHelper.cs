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

