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
        public static bool SUPERCRUISE = false;
        public static bool DROPPING = false;
        private double supercruiseTargetVel = 0.0d;

        double fsdSpeedMin = 0;
        double fsdSpeedMax = 0;
        public static double currentVel = 2500d;

        [KSPEvent(groupName = "FSDControls", guiActive = true, active = true, guiActiveEditor = false, guiName = "Toggle Supercruise", guiActiveUnfocused = false)]

        public void StopVessel()
        {
            DROPPING = true;
        }
        public void toggleSupercruise()
        {
            Quaternion vesselOrientation = FlightGlobals.ActiveVessel.GetTransform().rotation;
            List<Part> partsList = FlightGlobals.ActiveVessel.parts;
            CelestialBody mainBody = FlightGlobals.ActiveVessel.mainBody;

            if (!(FlightGlobals.ActiveVessel.altitude < (mainBody.minOrbitalDistance - mainBody.Radius)))//没有质量锁定时
            {


                SUPERCRUISE = !SUPERCRUISE;//先切换状态
                if (!SUPERCRUISE)//刚才正在超巡，准备退出超巡
                {
                    Vector3 flightVector = FlightGlobals.ActiveVessel.GetObtVelocity();
                    FlightGlobals.ActiveVessel.IgnoreGForces(1);
                    if (vessel.targetObject != null && vessel.targetObject.GetVessel() != null) //如果当前有飞船目标
                    {

                        if (Vector3.Distance(vessel.GetTransform().position, vessel.targetObject.GetTransform().position) < 100000d)//100km内目标锁定
                        {
                            ScreenMessages.PostScreenMessage("Destination Lock Engaged");
                            vessel.SetPosition(vessel.targetObject.GetTransform().position + new Vector3(2000, 2000, 2000));//位置放在5km外
                            FlightGlobals.ActiveVessel.ChangeWorldVelocity(-flightVector);
                            FlightGlobals.ActiveVessel.ChangeWorldVelocity(vessel.targetObject.GetVessel().GetObtVelocity());  //速度同步
                        }
                    }
                    else//没有飞船目标
                    {
                        ScreenMessages.PostScreenMessage("Dropping From Supercruise");
                        //FlightGlobals.ActiveVessel.ChangeWorldVelocity((vesselOrientation * new Vector3(0.0f, (float)previousVelocity, 0.0f)) - flightVector);//原地脱离
                        StopVessel();
                    }
                    foreach (Part part in partsList)
                        if (part.attachJoint != null)
                            part.attachJoint.SetUnbreakable(false, false);



                }

                else
                {//刚才没有超巡，因此即将进入超巡
                    ScreenMessages.PostScreenMessage("Supercruise Engaged");
                    foreach (Part part in partsList)
                    {
                        if (part.attachJoint != null)
                        {
                            part.attachJoint.SetUnbreakable(true, true);
                        }
                    }
                    currentVel = vessel.GetObtVelocity().magnitude;
                }                

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



        public void FixedUpdate()
        {
            try
            {
                Quaternion vesselOrientation = FlightGlobals.ActiveVessel.GetTransform().rotation;
                float throttleLevel = FlightInputHandler.state.mainThrottle;
                CelestialBody mainBody = FlightGlobals.ActiveVessel.mainBody;
                double minCuriseAltitude = mainBody.minOrbitalDistance - mainBody.Radius;

                if (SUPERCRUISE)
                {
                    if (FlightGlobals.ActiveVessel.altitude < (minCuriseAltitude + 100000.0d))
                    {
                        ScreenMessages.PostScreenMessage("Orbital Flight Engaged");
                    }

                    double graviteFactor = (FlightGlobals.ActiveVessel.altitude - minCuriseAltitude) / 10;
                    fsdSpeedMin = Math.Min(30000d, minCuriseAltitude / 30 + graviteFactor * 2.7);
                    fsdSpeedMax = Math.Max(minCuriseAltitude / 10 + graviteFactor * 10, 6000000000d); //20c


                    List<Part> partsList = FlightGlobals.ActiveVessel.parts;
                    if ((FlightGlobals.ActiveVessel.altitude < (minCuriseAltitude) && vessel.obt_speed > 10000))
                    {
                        SUPERCRUISE = false;
                        FlightGlobals.ActiveVessel.IgnoreGForces(1);
                        FlightGlobals.ActiveVessel.ChangeWorldVelocity(-flightVector);

                        foreach (Part part in partsList)
                        {
                            if (part.attachJoint != null)
                            {
                                part.attachJoint.SetUnbreakable(false, false);
                            }
                        }
                        ScreenMessages.PostScreenMessage("Emergency Drop: Too Close");
                    }
                    else if (FlightGlobals.ActiveVessel.altitude < (minCuriseAltitude))//低速进入滑翔
                    {
                        SUPERCRUISE = false;
                        foreach (Part part in partsList)
                        {
                            if (part.attachJoint != null)
                            {
                                part.attachJoint.SetUnbreakable(false, false);
                            }
                        }
                        ScreenMessages.PostScreenMessage("Glide Engaged, Dropping From Supercruising");
                    }
                    else
                    {
                        supercruiseTargetVel = 1000 * (Math.Exp(throttleLevel * 20) - 1) + fsdSpeedMin; //NonLinear
                        double speedfactor = supercruiseTargetVel / currentVel;
                        //设置飞船速度上下限
                        if (currentVel < fsdSpeedMin)
                            currentVel = currentVel * 1.013;//启动加速
                        else if (currentVel > fsdSpeedMax)
                            currentVel = currentVel / 1.016;//重力减速
                        else
                        {
                            if (currentVel < 300000000)//亚光速
                            {
                                if (speedfactor > 0.97 || speedfactor < 1.03)
                                    currentVel = supercruiseTargetVel;
                                else if ((speedfactor > 1.03) && !PauseMenu.isOpen)
                                    currentVel = throttleLevel > 0.95 ? currentVel * 1.01 : currentVel * 1.005; 
                                else if ((speedfactor < 0.97) && !PauseMenu.isOpen)
                                    currentVel = currentVel / 1.005;
                            }
                            else if (currentVel > 300000000 && currentVel < 2400000000)//8倍光速以下
                            {
                                if (speedfactor > 0.95 || speedfactor < 1.05)
                                    currentVel = supercruiseTargetVel;
                                else if ((speedfactor>1.05) && !PauseMenu.isOpen)
                                    currentVel = throttleLevel > 0.95 ? currentVel * 1.0014 : currentVel * 1.0007; 
                                else if ((speedfactor<0.95) && !PauseMenu.isOpen)
                                    currentVel = currentVel / 1.002;
                            }
                            else//8倍光速以上
                            {
                                if (speedfactor > 0.93 || speedfactor < 1.07)
                                    currentVel = supercruiseTargetVel;
                                else if ((speedfactor>1.07) && !PauseMenu.isOpen)
                                    currentVel = throttleLevel > 0.95 ? currentVel * 1.0006 : currentVel * 1.0003; 
                                else if ((speedfactor<0.93) && !PauseMenu.isOpen)
                                    currentVel = currentVel / 1.001;
                            }
                        }



                        //设置飞船速度
                        if (FlightGlobals.ActiveVessel == vessel)
                        {
                            FlightGlobals.ActiveVessel.IgnoreGForces(1);
                            vessel.ChangeWorldVelocity((vesselOrientation * new Vector3(0.0f, (float)currentVel, 0.0f)) - flightVector);
                        }

                        if (!PauseMenu.isOpen)
                        {
                            TimeWarp.SetRate(0, true, false);
                        }
                    }


                    if (vessel.targetObject != null && vessel.targetObject.GetVessel() != null)
                    {
                        if (Vector3.Distance(vessel.GetTransform().position, vessel.targetObject.GetTransform().position) < 100000d)//100km内目标锁定
                            ScreenMessages.PostScreenMessage("Ready for Disengage");
                    }
                }
                else if(DROPPING)
                {
                    //在这里把飞船减速到零
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
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
                    fsdSupercruise.SUPERCRUISE = true;
                    fsdSupercruise.currentVel = 2500d;
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