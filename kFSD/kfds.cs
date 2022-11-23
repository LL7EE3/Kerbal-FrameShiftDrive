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
            if (getState() != MLC && getState() != DRP)//û����������ʱ
            {
                if (SUPERCRUISING)//�ղ����ڳ�Ѳ��׼���˳���Ѳ
                {
                    FlightGlobals.ActiveVessel.IgnoreGForces(1);
                    if (vessel.targetObject != null && vessel.targetObject.GetVessel() != null) //�����ǰ�зɴ�Ŀ��
                    {
                        if (Vector3.Distance(vessel.GetTransform().position, vessel.targetObject.GetTransform().position) < 100000d)//100km��Ŀ������
                        {
                            ScreenMessages.PostScreenMessage("Destination Lock Engaged");
                            vessel.SetPosition(vessel.targetObject.GetTransform().position + new Vector3(2000, 2000, 2000));//λ�÷���5km��
                            FlightGlobals.ActiveVessel.ChangeWorldVelocity(-FlightGlobals.ActiveVessel.GetObtVelocity());
                            FlightGlobals.ActiveVessel.ChangeWorldVelocity(vessel.targetObject.GetVessel().GetObtVelocity());  //�ٶ�ͬ��
                        }
                    }
                    else//û�зɴ�Ŀ��
                    {
                        ScreenMessages.PostScreenMessage("Dropping From Supercruise");
                        StopVessel();
                        return;//��ǰ��������Ҫͣ����������л���Ѳ״̬
                    }
                    SetParts(false);
                }
                else
                {//�ղ�û�г�Ѳ����˼������볬Ѳ
                    ScreenMessages.PostScreenMessage("Supercruise Engaged");
                    SetParts(true);
                    CURSPEED = vessel.GetObtVelocity().magnitude;
                    if(getState()==DRP)//�ɴ�������߶���
                    {
                        TAKEOFF = true;
                        return;//��ǰ�˳���TAKEOFF�ǳ�Ѳ��һ��
                    }
                }
                SUPERCRUISING = !SUPERCRUISING;//����л�״̬����ʱֻӦ�ù��Է��л�Ѳ
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

        int getState()//fsd�ٶ�����������������
        {
            CelestialBody mainBody = FlightGlobals.ActiveVessel.mainBody;
            double radius = mainBody.Radius;
            double minOrbitalALT = mainBody.minOrbitalDistance - radius;
            double altitute = vessel.altitude;
            double radaraltitute = vessel.radarAltitude;
            double airpressure;

            if (altitute < minOrbitalALT + radius && vessel.GetObtVelocity().magnitude > 500000d)//���ٳ�������ʱֱ�ӳ�Ѳ��������
            {
                ScreenMessages.PostScreenMessage("Emergency Drop: Too Close");
                return MLC;
            }

            if (mainBody.atmosphereDepth > 0)
            {//�д�������
                airpressure = mainBody.GetPressureAtm(altitute);
                //�������򸽽�ʱΪ�������OC���SC
                if (altitute>minOrbitalALT && altitute <= minOrbitalALT + radius)//������и߶�OCΪ�״�߶�25km������ֱ���߶�
                {
                    FSDMIN = 2500 + 27500d * (altitute - minOrbitalALT) / radius;//����30000m/s���Լ��ٵ�2500m/s
                    FSDMAX = 2500 + Math.Pow(altitute - minOrbitalALT, 2) / 40404d;//����ں���100kmʱ�ṩ250000m/s������ٶ�
                    return OC;
                }
                else if (altitute > minOrbitalALT + radius)//��̫��SC
                {

                    FSDMIN = 30000d;
                    FSDMAX = Math.Min(Math.Exp(30000 + (altitute + 210000) / 25000), 6000000000d);
                    if (vessel.targetObject != null)
                    {
                        FSDMAX = //����Ŀ������20c
                        Math.Min(Vector3.Distance(vessel.GetTransform().position, vessel.targetObject.GetTransform().position) / 7, FSDMAX);
                    }

                    return SC;
                }
                else if (radaraltitute <= 1000 || airpressure >= 0.001)//���볬Ѳ����Ϊ�״�߶�С��1000m����ѹ����0.001atm
                {
                    FSDMAX = 0;
                    FSDMIN = 0;
                    return MLC;
                }
                else if (altitute <= minOrbitalALT && airpressure <0.001)//��������Ϊ���������ѹ��С��0.001atm�����뻬��Glide״̬
                {
                    if (TAKEOFF)
                    {
                        //���ʱ�ٶ��ɸ߶Ⱦ������Ҳ��ɵ�
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
            {//�޴�������
             //�������򸽽�ʱΪ�������OC���SC
                if (radaraltitute > 25000 && altitute <= minOrbitalALT + radius)//������и߶�OCΪ�״�߶�25km������ֱ���߶�
                {
                    FSDMIN = 2500 + 27500d * (altitute  - minOrbitalALT) / radius;//����30000m/s���Լ��ٵ�2500m/s
                    FSDMAX = 2500 + Math.Pow(altitute  - minOrbitalALT, 2) / 40404d;//����ں���100kmʱ�ṩ250000m/s������ٶ�
                    return OC;
                }
                else if (altitute > minOrbitalALT + radius)//��̫��SC
                {
                    FSDMIN = 30000d;
                    FSDMAX = Math.Min(Math.Exp(30000 + (altitute + 210000) / 25000),6000000000d);
                    if (vessel.targetObject != null)
                    {
                        FSDMAX = //����Ŀ������20c
                        Math.Min(Vector3.Distance(vessel.GetTransform().position, vessel.targetObject.GetTransform().position) / 7, FSDMAX);
                    }

                    return SC;
                }
                else if (radaraltitute <= 1000)//��������MLCΪ�״�߶�1km��
                {
                    FSDMAX = 0;
                    FSDMIN = 0;
                    return MLC;
                }
                else if (radaraltitute > 1000 && radaraltitute <= 25000)//����߶�DRPΪ25km������Glide���״�߶�1km
                {
                    if(TAKEOFF)
                    {
                        //���ʱ�ٶ��ɸ߶Ⱦ������Ҳ��ɵ�
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
                        if (Vector3.Distance(vessel.GetTransform().position, vessel.targetObject.GetTransform().position) < 100000d)//100km��Ŀ������
                            ScreenMessages.PostScreenMessage("Ready for Disengage");
                    //===================

                    TGASPEED= Math.Min(FSDMAX, 1000 * (Math.Exp(throttleLevel * 20) - 1) + FSDMIN); //����Ŀ���ٶȣ�Ӧ��С�������ٶ�
                    switch (getState())//����������CURSPEED���Ժ���Դ˵�����Ϸ��ɴ��ٶ�
                    {
                        
                        
                        case SC:
                            {
                                if (CURSPEED <= TGASPEED)//��Ҫ����
                                    CURSPEED += Math.Min((TGASPEED - CURSPEED) / 150d, CURSPEED / 150d);//ÿ����ٶ�Ϊ��ֵ��10%��ǰ�ٶȵ�10%�бȽ�С�ģ��������ٲ���̫�죬ͬʱ�ٶ��ٽ�ʱ�����
                                else
                                    CURSPEED -= Math.Min((CURSPEED - TGASPEED) / 80d, CURSPEED / 80d);//ͬ��
                                break;
                            }
                        case OC:
                            {//��������ٶ�����������
                                ScreenMessages.PostScreenMessage("Orbital Flight Engaged");
                                if (CURSPEED <= TGASPEED)//��Ҫ����
                                    CURSPEED += Math.Min((TGASPEED - CURSPEED) / 40d, CURSPEED / 40d);
                                else
                                    CURSPEED -= Math.Min((CURSPEED - TGASPEED) / 20d, CURSPEED / 20d);
                                break;
                            }
                        case DRP:
                            {//Glide�ٶ�����������
                                ScreenMessages.PostScreenMessage("Glide Engaged, Dropping From Supercruising");
                                CURSPEED = 2500d;
                                EngadeGlide();
                                break;
                            }
                        case MLC:
                            {//��������������
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
                        CURSPEED -= CURSPEED / 30d;//ÿ����ٺܶ�
                    }
                    if (vessel.GetObtVelocity().magnitude < 5)
                    {
                        ScreenMessages.PostScreenMessage("Dropped From Supercruise");
                        vessel.ChangeWorldVelocity(-vessel.GetObtVelocity());
                        DROPPING = false;
                        SetParts(false);//�˳���Ѳ���ָ��ɴ��ṹ
                    }
                }
                else if(GLIDING)
                {
                    //CURSPEED��ǰ���ù�
                    ScreenMessages.PostScreenMessage("Gliding, DO NOT Pull Up");
                    if (getState()!=DRP)//���͵�MLC�˳���ֱ�ٶȴ���0
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
                    if (CURSPEED <= TGASPEED)//��Ҫ����
                        CURSPEED += Math.Min((TGASPEED - CURSPEED) / 1000d, CURSPEED / 1000d);
                    else
                        CURSPEED -= Math.Min((TGASPEED - CURSPEED) / 600d, CURSPEED / 600d);

                    if (getState() != DRP || vessel.GetTransform().eulerAngles.x < 0)//������OC�˳������Ǵ���0���˴���ŷ���Ǵ���
                    {
                        StopVessel();
                    }
                }
                else//�ڳ���ռ���Է���
                {
                    return;
                }

                //���÷ɴ��ٶ�
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