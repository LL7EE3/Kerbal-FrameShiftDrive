using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace kspFSD
{

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class fsdSupercruise : PartModule
    {
        const int MLC = -1;
        const int DRP = 3;
        const int OC = 5;
        const int SC = 10;

        public static bool SUPERCRUISING = false;
        public static bool DROPPING = false;
        public static bool GLIDING = false;
        public static bool TAKEOFF = false;

        static double TGASPEED = 0;
        static double CURSPEED = 0;
        static double FSDMAX = 0;
        static double FSDMIN = 0;

        [KSPEvent(groupName = "FSDControls", guiActive = true, active = true, guiActiveEditor = false, guiName = "Toggle Supercruise", guiActiveUnfocused = false)]

        public void StopVessel()
        {
            DROPPING = true;
            GLIDING = false;
            SUPERCRUISING = false;
            TAKEOFF= false;
        }
        public void EngadeGlide()
        {
            DROPPING = false;
            GLIDING = true;
            SUPERCRUISING = false;
            TAKEOFF = false;
        }
        public void toggleSupercruise()
        {
            if (GLIDING)
            {
                StopVessel();
                ScreenMessages.PostScreenMessage("Glide Failed");
                return;
            }
            if (getState() != MLC && getState() != DRP)//没有质量锁定时
            {
                if (SUPERCRUISING)//刚才正在超巡，准备退出超巡
                {
                    FlightGlobals.ActiveVessel.IgnoreGForces(1);
                    if (vessel.targetObject != null && vessel.targetObject.GetVessel() != null) //如果当前有飞船目标
                    {
                        if (Vector3.Distance(vessel.GetTransform().position, vessel.targetObject.GetTransform().position) < 100000d)//100km内目标锁定
                        {
                            ScreenMessages.PostScreenMessage("Destination Lock Engaged");
                            vessel.SetPosition(vessel.targetObject.GetTransform().position + new Vector3(2000, 2000, 2000));//位置放在5km外
                            FlightGlobals.ActiveVessel.ChangeWorldVelocity(-FlightGlobals.ActiveVessel.GetObtVelocity());
                            FlightGlobals.ActiveVessel.ChangeWorldVelocity(vessel.targetObject.GetVessel().GetObtVelocity());  //速度同步
                        }
                    }
                    else//没有飞船目标
                    {
                        ScreenMessages.PostScreenMessage("Dropping From Supercruise");
                        StopVessel();
                        return;//提前跳出，需要停船函数里会切换超巡状态
                    }
                    SetParts(false);
                }
                else
                {//刚才没有超巡，因此即将进入超巡
                    ScreenMessages.PostScreenMessage("Supercruise Engaged");
                    SetParts(true);
                    CURSPEED = vessel.GetObtVelocity().magnitude;
                    if(getState()==DRP)//飞船在脱离高度下
                    {
                        TAKEOFF = true;
                        return;//提前退出，TAKEOFF是超巡的一种
                    }
                }
                SUPERCRUISING = !SUPERCRUISING;//最后切换状态，这时只应该惯性飞行或超巡
                GLIDING = false;
                DROPPING= false;
            }
            else
            {
                ScreenMessages.PostScreenMessage("Frame Shift Cancled: Mass Lock");
            }
        }

        [KSPAction(guiName = "Toggle Supercruise", isPersistent = true)]
        public void toggleSupercruiseAction(KSPActionParam param)
        {
            toggleSupercruise();
        }

        int getState()//fsd速度上下限在这里设置
        {
            CelestialBody mainBody = FlightGlobals.ActiveVessel.mainBody;
            double radius = mainBody.Radius;
            double minOrbitalALT = mainBody.minOrbitalDistance - radius;
            double altitute = vessel.altitude;
            double radaraltitute = vessel.radarAltitude;
            double airpressure;

            if (altitute < minOrbitalALT + radius && vessel.GetObtVelocity().magnitude > 500000d)//高速冲向星球时直接超巡紧急脱离
            {
                ScreenMessages.PostScreenMessage("Emergency Drop: Too Close");
                return MLC;
            }

            if (mainBody.atmosphereDepth > 0)
            {//有大气行星
                airpressure = mainBody.GetPressureAtm(altitute);
                //不在星球附近时为轨道飞行OC或宇航SC
                if (altitute>minOrbitalALT && altitute <= minOrbitalALT + radius)//轨道飞行高度OC为雷达高度25km至星球直径高度
                {
                    FSDMIN = 2500 + 27500d * (altitute - minOrbitalALT) / radius;//这会从30000m/s线性减少到2500m/s
                    FSDMAX = 2500 + Math.Pow(altitute - minOrbitalALT, 2) / 40404d;//这会在海拔100km时提供250000m/s的最大速度
                    return OC;
                }
                else if (altitute > minOrbitalALT + radius)//在太空SC
                {

                    FSDMIN = 30000d;
                    FSDMAX = Math.Min(Math.Exp(30000 + (altitute + 210000) / 25000), 6000000000d);
                    if (vessel.targetObject != null)
                    {
                        FSDMAX = //七秒目标距离或20c
                        Math.Min(Vector3.Distance(vessel.GetTransform().position, vessel.targetObject.GetTransform().position) / 7, FSDMAX);
                    }

                    return SC;
                }
                else if (radaraltitute <= 1000 || airpressure >= 0.001)//脱离超巡条件为雷达高度小于1000m或气压大于0.001atm
                {
                    FSDMAX = 0;
                    FSDMIN = 0;
                    return MLC;
                }
                else if (altitute <= minOrbitalALT && airpressure <0.001)//滑行条件为进入大气且压力小于0.001atm，进入滑行Glide状态
                {
                    if (TAKEOFF)
                    {
                        //起飞时速度由高度决定，且不可调
                        FSDMAX = radaraltitute / 10;
                        FSDMIN = FSDMAX;
                    }
                    else
                    {
                        FSDMAX = 2500;
                        FSDMIN = 2500;
                        GLIDING = true;
                    }
                    return DRP;
                }
            }
            else
            {//无大气行星
             //不在星球附近时为轨道飞行OC或宇航SC
                if (radaraltitute > 25000 && altitute <= minOrbitalALT + radius)//轨道飞行高度OC为雷达高度25km至星球直径高度
                {
                    FSDMIN = 2500 + 27500d * (altitute  - minOrbitalALT) / radius;//这会从30000m/s线性减少到2500m/s
                    FSDMAX = 2500 + Math.Pow(altitute  - minOrbitalALT, 2) / 40404d;//这会在海拔100km时提供250000m/s的最大速度
                    return OC;
                }
                else if (altitute > minOrbitalALT + radius)//在太空SC
                {
                    FSDMIN = 30000d;
                    FSDMAX = Math.Min(Math.Exp(30000 + (altitute + 210000) / 25000),6000000000d);
                    if (vessel.targetObject != null)
                    {
                        FSDMAX = //七秒目标距离或20c
                        Math.Min(Vector3.Distance(vessel.GetTransform().position, vessel.targetObject.GetTransform().position) / 7, FSDMAX);
                    }

                    return SC;
                }
                else if (radaraltitute <= 1000)//质量锁定MLC为雷达高度1km内
                {
                    FSDMAX = 0;
                    FSDMIN = 0;
                    return MLC;
                }
                else if (radaraltitute > 1000 && radaraltitute <= 25000)//脱离高度DRP为25km，滑行Glide至雷达高度1km
                {
                    if(TAKEOFF)
                    {
                        //起飞时速度由高度决定，且不可调
                        FSDMAX = radaraltitute / 10;
                        FSDMIN = FSDMAX;
                    }
                    else
                    {
                        FSDMAX = 2500;
                        FSDMIN = 2500;
                        GLIDING = true;
                    }
                    return DRP;
                }
            }

            //default
            FSDMIN = 30000d;
            FSDMAX = 6000000000d;
            return SC;


        }

        public void SetParts(bool b)
        {
            List<Part> partsList = FlightGlobals.ActiveVessel.parts;
            foreach (Part part in partsList)
            {
                if (part.attachJoint != null)
                {
                    part.attachJoint.SetUnbreakable(b, b);
                }
            }
        }


        public void FixedUpdate()
        {
            try
            {
                Quaternion vesselOrientation = FlightGlobals.ActiveVessel.GetTransform().rotation;
                float throttleLevel = FlightInputHandler.state.mainThrottle;
                CelestialBody mainBody = FlightGlobals.ActiveVessel.mainBody;
                double radius = mainBody.Radius;
                double minOrbitalALT = mainBody.minOrbitalDistance;
                double altitute = vessel.altitude;
                double radaraltitute = vessel.radarAltitude;

                if (SUPERCRUISING)
                {
                    //=================
                    if (vessel.targetObject != null && vessel.targetObject.GetVessel() != null)
                        if (Vector3.Distance(vessel.GetTransform().position, vessel.targetObject.GetTransform().position) < 100000d)//100km内目标锁定
                            ScreenMessages.PostScreenMessage("Ready for Disengage");
                    //===================

                    TGASPEED= Math.Min(FSDMAX, 1000 * (Math.Exp(throttleLevel * 20) - 1) + FSDMIN); //设置目标速度，应该小于限制速度
                    switch (getState())//在这里设置CURSPEED，稍后会以此调整游戏里飞船速度
                    {
                        
                        
                        case SC:
                            {
                                if (CURSPEED <= TGASPEED)//需要加速
                                    CURSPEED += Math.Min((TGASPEED - CURSPEED) / 150d, CURSPEED / 150d);//每秒加速度为差值的10%或当前速度的10%中比较小的，这样加速不会太快，同时速度临近时会变慢
                                else
                                    CURSPEED -= Math.Min((CURSPEED - TGASPEED) / 80d, CURSPEED / 80d);//同理
                                break;
                            }
                        case OC:
                            {//轨道飞行速度在这里设置
                                ScreenMessages.PostScreenMessage("Orbital Flight Engaged");
                                if (CURSPEED <= TGASPEED)//需要加速
                                    CURSPEED += Math.Min((TGASPEED - CURSPEED) / 40d, CURSPEED / 40d);
                                else
                                    CURSPEED -= Math.Min((CURSPEED - TGASPEED) / 20d, CURSPEED / 20d);
                                break;
                            }
                        case DRP:
                            {//Glide速度在这里设置
                                ScreenMessages.PostScreenMessage("Glide Engaged, Dropping From Supercruising");
                                CURSPEED = 2500d;
                                EngadeGlide();
                                break;
                            }
                        case MLC:
                            {//质量锁定，脱离
                                StopVessel();
                                break;
                            }
                        default:
                            {
                                break;
                            }
                    }
               }
                else if(DROPPING)
                {
                    {
                        CURSPEED -= CURSPEED / 30d;//每秒减少很多
                    }
                    if (vessel.GetObtVelocity().magnitude < 5)
                    {
                        ScreenMessages.PostScreenMessage("Dropped From Supercruise");
                        vessel.ChangeWorldVelocity(-vessel.GetObtVelocity());
                        DROPPING = false;
                        SetParts(false);//退出超巡，恢复飞船结构
                    }
                }
                else if(GLIDING)
                {
                    //CURSPEED先前设置过
                    ScreenMessages.PostScreenMessage("Gliding, DO NOT Pull Up");
                    if (getState()!=DRP)//降低到MLC退出或垂直速度大于0
                    {
                        if(vessel.verticalSpeed>0)
                            ScreenMessages.PostScreenMessage("Glide Failed");
                        else
                            ScreenMessages.PostScreenMessage("Glide Competed");
                        StopVessel();
                    }
                }
                else if(TAKEOFF)
                {
                    ScreenMessages.PostScreenMessage("Taking Off, Pull Up, Don't Sink");
                    if (CURSPEED <= TGASPEED)//需要加速
                        CURSPEED += Math.Min((TGASPEED - CURSPEED) / 1000d, CURSPEED / 1000d);
                    else
                        CURSPEED -= Math.Min((TGASPEED - CURSPEED) / 600d, CURSPEED / 600d);

                    if (getState() != DRP || vessel.GetTransform().eulerAngles.x < 0)//爬升到OC退出或仰角大于0，此处的欧拉角存疑
                    {
                        StopVessel();
                    }
                }
                else//在常规空间惯性飞行
                {
                    return;
                }

                //设置飞船速度
                if (FlightGlobals.ActiveVessel == vessel)
                {
                    FlightGlobals.ActiveVessel.IgnoreGForces(1);
                    vessel.ChangeWorldVelocity((vesselOrientation * new Vector3(0.0f, (float)CURSPEED, 0.0f)) - vessel.GetObtVelocity());
                }
                if (!PauseMenu.isOpen)
                {
                    TimeWarp.SetRate(0, true, false);
                }
            }
            catch (Exception ex)
            {
                //Debug.Log(ex);
            }
        }
    }
    public class fsdHyperspaceJump : PartModule
    {
        [KSPEvent(groupName = "FSDControls", guiActive = true, active = true, guiActiveEditor = false, guiName = "Hyperspace Jump", guiActiveUnfocused = false)]
        public void hyperspaceJump()
        {
            try
            {
                CelestialBody targetDestination = FlightGlobals.ActiveVessel.patchedConicSolver.targetBody;
                CelestialBody gravityWell = FlightGlobals.ActiveVessel.mainBody;
                if (targetDestination != null && (FlightGlobals.ActiveVessel.altitude > (gravityWell.minOrbitalDistance - gravityWell.Radius + 2500.0d)))
                {
                    Vector3 movementVector = FlightGlobals.ActiveVessel.GetObtVelocity();
                    List<Part> partsList = FlightGlobals.ActiveVessel.parts;
                    foreach (Part part in partsList)
                    {
                        if (part.attachJoint != null)
                        {
                            part.attachJoint.SetUnbreakable(true, true);
                        }
                    }
                    FlightGlobals.ActiveVessel.ChangeWorldVelocity(-movementVector);
                    Orbit deployOrbit = new Orbit(0, 0, targetDestination.Radius * 2, 0, 0, 0, 0, targetDestination);
                    Vector3 deployPosition = deployOrbit.getPositionAtUT(Planetarium.GetUniversalTime());
                    OrbitPhysicsManager.HoldVesselUnpack(60);
                    FlightGlobals.ActiveVessel.IgnoreGForces(10);
                    FlightGlobals.ActiveVessel.IgnoreSpeed(10);
                    FlightGlobals.ActiveVessel.SetPosition(deployPosition);
                    FlightGlobals.ActiveVessel.ChangeWorldVelocity(-movementVector);
                    fsdSupercruise.SUPERCRUISING = true;
                    foreach (Part part in partsList)
                    {
                        if (part.attachJoint != null)
                        {
                            part.attachJoint.SetUnbreakable(false, false);
                        }
                    }
                    FlightGlobals.ActiveVessel.rootPart.AimCamera();
                    FloatingOrigin.ResetTerrainShaderOffset();
                    FloatingOrigin.SetOffset(FlightGlobals.ActiveVessel.GetWorldPos3D());
                }
            }
            catch (Exception ex)
            { Debug.Log(ex); }

        }
        [KSPAction(guiName = "Hyperspace Jump", isPersistent = true)]
        public void performJump(KSPActionParam param)
        {
            hyperspaceJump();
        }
    }
}