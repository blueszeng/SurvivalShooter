﻿using CBFrame.Core;
using CBFrame.Sys;
using Frame;
using KBEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameEntity : MonoBehaviour {

    public bool isPlayer = false;
    public bool isAvatar = false;
    public bool entityEnabled = true;
    public  KBEngine.Entity entity;

    //----------------------------
    private UInt32 readFramePos = 0;

    public static float playTime = 1 / 30.0f; // 33 s
    private float FrameDuration = 0f;
    private Vector3 destPosition = Vector3.zero;
    private float destDuration = 0.0f;
    private int thresholdFrame = 1;
    public float emptyFramesTime = 0f;
    public int thresholdMaxFrame = 30;

    private float Speed = 10.0f;
    //----------------------------
    public class FrameData
    {
        public float duration;
        public List<ENTITY_DATA> operation = new List<ENTITY_DATA>();
    }

    public Queue<KeyValuePair<UInt32, FrameData>> framePool = new Queue<KeyValuePair<UInt32, FrameData>>();

    public FrameData lastFrameData = null;
    private UInt32 curreFrameId = 0;

    public float DestDuration
    {
        get
        {
            return destDuration;
        }

        set
        {
            destDuration = value;
        }
    }

    public int ThresholdFrame
    {
        get
        {
            return thresholdFrame;
        }

        set
        {
            if (value < 1)
            {
                thresholdFrame = 1;
            }
            else if (value > thresholdMaxFrame)
            {
                thresholdFrame = thresholdMaxFrame;
            }
            else
            {
                thresholdFrame = value;
            }
        }
    }

    //----------------------------
    public void entityEnable()
    {
        entityEnabled = true;
    }

    public void entityDisable()
    {
        entityEnabled = false;
    }

    // Use this for initialization
    void Start ()
    {
 //       CBGlobalEventDispatcher.Instance.AddEventListener<FRAME_DATA>((int)EVENT_ID.EVENT_FRAME_TICK, onUpdateTick);
        Debug.LogError("GameEntity.start." + transform.name);
    }

    void OnGUI()
    {
        if (Camera.main == null || transform.name == "")
            return;

        //根据NPC头顶的3D坐标换算成它在2D屏幕中的坐标     
        Vector2 uiposition = Camera.main.WorldToScreenPoint(transform.position);

        //得到真实NPC头顶的2D坐标
        uiposition = new Vector2(uiposition.x, Screen.height - uiposition.y);

        //计算NPC名称的宽高
        Vector2 nameSize = GUI.skin.label.CalcSize(new GUIContent(transform.name));


        GUIStyle fontStyle = new GUIStyle();
        fontStyle.normal.background = null;             //设置背景填充  
        fontStyle.normal.textColor = Color.yellow;      //设置字体颜色  
        fontStyle.fontSize = (int)(15.0 * gameObject.transform.localScale.x);
        fontStyle.alignment = TextAnchor.MiddleCenter;

        //绘制NPC名称
        GUI.Label(new Rect(uiposition.x - (nameSize.x / 4), uiposition.y - nameSize.y*4, nameSize.x, nameSize.y), transform.name, fontStyle);
    }

    public void onUpdateTick(FRAME_DATA frameMsg)
    {
        curreFrameId = frameMsg.frameid;
        Debug.Log("id:"+ entity.id +",frameid:" + frameMsg.frameid + ",----------onRecieveFrame tick : " + DateTime.Now.TimeOfDay.ToString());

        bool isEmptyFrame = true;

        for (int i = 0; i < frameMsg.operation.Count; i++)
        {
            var oper = frameMsg.operation[i];
            Debug.Log("operation id:" + oper.entityid);

            if (oper.entityid != entity.id)
            {
                continue;
            }

            isEmptyFrame = false;
            var data = new FrameData();
            data.duration = playTime;
            data.operation.Add(oper);

            framePool.Enqueue(new KeyValuePair<UInt32, FrameData>(curreFrameId, data));
            lastFrameData = data;
        }

        if (isEmptyFrame && lastFrameData != null)
        {
            framePool.Enqueue(new KeyValuePair<UInt32, FrameData>(curreFrameId, lastFrameData));
        }

    }

    void onReadFrame()
    {
//        Debug.LogError("id:" + entity.id + ",readFramePos:" + readFramePos + ",frame_list.Count:" + PlayerData.Instance.frame_list.Count);
        while(readFramePos < PlayerData.Instance.frame_list.Count)
        {
            ++readFramePos;

            var frameData = PlayerData.Instance.frame_list[readFramePos];
            onUpdateTick(frameData);
        }
    }

    // Update is called once per frame
    void Update () {

        onReadFrame();


        if (!isAvatar)
            return;

        float dis = Vector3.Distance(transform.position, destPosition);
        float currSpeed = DestDuration <=0 ? Speed : (Speed * playTime / DestDuration);

        if(dis <= currSpeed * Time.deltaTime)
        {
            transform.position = destPosition;
//           Debug.LogError("----------diff time------------------:" + (playTime - FrameDuration));
        }
        else
        {
            Vector3 tempDirection = destPosition - transform.position;

            transform.position += tempDirection.normalized * currSpeed * Time.deltaTime;
        }
        


        FrameDuration += Time.deltaTime;

        if (FrameDuration >= DestDuration)
        {
            transform.position = destPosition;

            if (framePool.Count > 0)
            {   
                DestDuration = playTime / (  framePool.Count <= ThresholdFrame ? 1: framePool.Count/ ThresholdFrame);

//                 if(framePool.Count > 8)
//                     Debug.LogError("framePool.Count too big:" + +framePool.Count);

                var framedata = framePool.Dequeue();

                emptyFramesTime = 0.0f;
 //               ThresholdFrame -= 1;

 //               Debug.Log("frame.id:"+ framedata.Key + " framePool.Count" + framePool.Count );

                Vector3 movement = Vector3.zero;
                double point = 0.0;
                foreach (var item in framedata.Value.operation)
                {
                    if (item.cmd_type != (UInt32)CMD.USER)
                    {
                        continue;
                    }

                    FrameUser msg = FrameProto.decode(item) as FrameUser;
                    movement = msg.movement;
                    point = msg.d_point;
                }
 //               Debug.Log("d_point:" + point);

                destPosition += Speed * movement * playTime;

                FrameDuration = 0.0f;
            }
            else if(lastFrameData != null)
            {
//                Debug.Log("emptyFramesTime," + emptyFramesTime);

                emptyFramesTime += Time.deltaTime;

                if(emptyFramesTime >= playTime)
                {
//                    ThresholdFrame = (int)(emptyFramesTime / playTime);

 //                   Debug.LogError("one frame time out,emptyFramesTime:" + emptyFramesTime + ",ThresholdFrame:"+ ThresholdFrame);
                }
            }
        }
    }
}
