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
