using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System;
using System.Collections.Generic;
using Higler.Classes.HardwareUnderFlying;
using Higler.Classes.DeskOverlay;
using System.Windows.Threading;

namespace Higler.Classes
{


    public class Operate
    {

        //
        protected ReSourceHardware hardware = new ReSourceHardware();
        protected int zoneNum;
        internal ReSourceHardware Hardware  //资源锁，防止冲突
        {
            get { return hardware; }
            set { hardware = value; }
        }

        //写日志
        protected string headtxt = "";
        protected string msg = "";
        protected RunningStateInformation rs;

        protected int smallAirCapacity = DevicePara.KeepAir;   //所吸的空气量
        protected int bigAirCapacity = DevicePara.SeparateAir;
        //protected int appendLiquidCapacity = 40;   //多吸40ul
 //       protected int appendLiquidCapacity = 60;   //多吸60ul

        protected int[] PumpHaveSlide = {0,0};

        protected const int Needle1 = 0;
        protected const int Needle2 = 1;
        protected const int NeedleMax = 1;
 //       protected const int Needle1 = 0;
 //       protected const int Needle2 = 1;
 //       protected const int Needle1 = 0;
 //       protected const int Needle2 = 1;
 //       protected const int Z3 = 2;
  //      protected const int Needle1 = 0;
  //      protected const int Needle2 = 1;

        protected const int Bottle = 0;
        protected const int Bucket = 1;

        protected const int Shallow = 1;
        protected const int Deeper = 2;
        protected const int Washed = 0;

        protected const bool Electric = true;
        protected const bool NoElectric = false;


        protected const int CoolWater = 0;
        protected const int RestoreLiquid = 1;

        protected const int WashNeedleTime = 45;  //45s

        int ReagentMaxColumn;

        public event ReagentUpdateEvent UpdateReagent;
        public event ReagentEmptyEvent ReagentEmpty;
        public event MessageOccuredEvent MessageOccured;

        //暂停事件
        public delegate void ThreadPause();
        public event ThreadPause PauseEvent;

        protected StepDetailInformation step;
        protected int Priority = 0;
        protected UInt32 OrderID = 0;
        protected bool DisplayTimeout = false;
//        protected BottleState CurrentBottleState ;
        protected BottleState[] currentBottleStates = new BottleState[2] { new BottleState(BottleState.NOMAL), new BottleState(BottleState.NOMAL) };  

        public void DoNothing()
        {
            //	donothing
        }
        public Operate()
        {
            
        }

        public Operate(int Num,int  Priority , bool DisplayTimeout, UInt32 OrderID )
        {
            ReagentMaxColumn = Location.MaxColumnsBottle + Location.MaxClomnsSlot;
            zoneNum = Num;
            this.Priority = Priority;
            this.DisplayTimeout = DisplayTimeout;  //是否显示
            this.OrderID = OrderID;
            hardware.IncubatorNum = zoneNum;
            if (zoneNum == 0)
            {
                headtxt = "A线程";
                rs = SystemManage.SysManage.RunstateMonitor[0];
   //             SystemManage.SysManage.setHardwareHost("A线程");
            }
            else if (zoneNum == 1)
            {
                headtxt = "B线程";
                rs = SystemManage.SysManage.RunstateMonitor[1];
  //              SystemManage.SysManage.setHardwareHost("B线程");
            }
            else if (zoneNum == 2)
            {
                headtxt = "C线程";
                rs = SystemManage.SysManage.RunstateMonitor[2];
 //               SystemManage.SysManage.setHardwareHost("C线程");
            }
            else if (zoneNum == 3)
            {
                headtxt = "D线程";
                rs = SystemManage.SysManage.RunstateMonitor[3];
  //              SystemManage.SysManage.setHardwareHost("D线程");
            }
            else
            {
                rs = new RunningStateInformation();
            }
        }

 
        //执行
        virtual public bool execute()
        {
            return true;
        }

/// <summary>
/// 显示信息对话框
/// </summary>
/// <param name="str"></param>
        protected ErrorMsgResult DisPlayErrorMsg(string str,string title = "错误",MessageInfoButton button =MessageInfoButton.NO, MessageInfoBImage image = MessageInfoBImage.ERROR)
        {
 //           throw new NotImplementedException();
   //         MessageBox.Show(headtxt + str);

            RecordMsg(str, 1);
            return ErrorMessage.DisplayMessage(str, title, button, image);


            
        }

        /// <summary>
        /// 记录数据
        /// </summary>
        /// <param name="msg"></param>
        protected void RecordMsg(string msg, int isError = 0, bool writeLog = true, bool OperatingRecord = true, bool CurrentStaus = true)
        {
            String recordMsg;
            
            if (isError == 0)
            {
                recordMsg = headtxt + msg;
    
            }
            else
            {
                recordMsg = "###"  + headtxt + msg;
  //              DailyRecord.writerecord(recordMsg);
            }

            MessageOccured(recordMsg);            
        }
        /// <summary>
        /// 双针取液
        /// </summary>
        /// <param name="p"></param>
        /// <param name="vol"></param>
        /// <param name="RemainSlideTotal"></param>
        /// <param name="usingReagent"></param>
        /// <returns></returns>
      //    protected bool DoubleNeedlePickUp(int vol, ref int RemainSlideTotal, UsingReagentInformation[] usingReagent)
        protected bool DoubleNeedlePickUp(String strReagent, int vol, ref int UnUseSlideTotal,int appendLiquidCapacity = 40)   
     {
    
              RecordMsg("开始双针取液。");
              Arm[] armZ = {hardware.ArmZ1,hardware.ArmZ2};

              SyringePump[] syringePump = { hardware.syringePump[Needle1], hardware.syringePump[Needle2] };

              ObservableCollection<UsingReagentInformation> ReagentColList;
            //重试
            do
            {
                //查询符合条件的所有试剂列表
                ObservableCollection<UsingReagentInformation> findReagentList = null;
                findReagentList = FindReagent(strReagent);
                //查找在一列中的试剂
                ReagentColList = getReagentListInColumn(findReagentList,Location.MaxRowsBottle);

                if ((ReagentColList == null) || (ReagentColList.Count == 0))
                  {
                      //               MessageBox.Show("在设备上无法找到可用的试剂。");
                      RecordMsg("在设备上无法找到可用的试剂——" + strReagent, 1, true, true, false);

                      //                     ErrorMsgResult.Abort == DisPlayErrorMsg("在设备上已无法找到可用的试剂,请添加试剂后重试。", "警告", MessageInfoButton.RETRYNO, MessageInfoBImage.WARNING))
                      DisPlayErrorMsg("在设备上已无法找到可用的该试剂类型，("+strReagent+")该类型试剂可能已用完！\r\n请添加试剂后重试。", "警告", MessageInfoButton.YES, MessageInfoBImage.OK);

                      //暂停事件
 //                     PauseEvent();
                      CreatePauseEvent();
                      hardware.Sleep(100);
  //                    Pause();
                  }
            } while ((ReagentColList == null) || (ReagentColList.Count == 0));
         
               int amount = ReagentColList.Count;   //这一列有几个试剂瓶

             //将需要加液的片子均分,两根针的算法，如果多针，需设计通用算法
               int []UnusedSlide = new int[2];

               if (UnUseSlideTotal % 2 == 0)
               {
                   UnusedSlide[Needle1] = UnUseSlideTotal / 2;
                   UnusedSlide[Needle2] = UnUseSlideTotal / 2;
               }
               else
               {
                   UnusedSlide[Needle1] = UnUseSlideTotal / 2 + 1;
                   UnusedSlide[Needle2] = UnUseSlideTotal / 2;
               }
      
               ReagentInformation reagent = DataCollection.GetReagent("ReagentID", ReagentColList[0].ReagentID);//获取试剂的完整信息
 
              /////////////////////////////////////////////////////////////////////////
              //这一列只有一个试剂瓶，并且不是试剂槽（试剂槽可以双针取）,先Y1取，再Y2取
               BottleInformation bottle = DataCollection.GetSpecialBottle("BottleID",reagent.BottleID);
               if ((amount == 1) && !(bottle.PlaceZone.Equals("试剂槽区")))
               {
                   for (int NeedleIndex = Needle1; NeedleIndex <= NeedleMax; NeedleIndex++)
                   {

                       if (NeedleIndex > Needle1)
                       {
                           UnusedSlide[NeedleIndex] = UnusedSlide[NeedleIndex] + UnusedSlide[Needle1];
                       }
                       //重试取液 Z1
                       bool isRetry = true  ;
                       while(isRetry)
                       {
                           try
                           {
                               SingleNeedlePickUp(NeedleIndex, strReagent, vol, ref UnusedSlide[NeedleIndex], appendLiquidCapacity);
                           }
                           catch(HardwareException ex)
                           {
                               String errorInfo = "错误信息：" + ex.Message + "\t\n错误源：" + ex.Source + "\t\n错误原因：" + ex.CauseofError;
                               if (DisPlayErrorMsg(" 取液失败.\t\n" +errorInfo + "\t\n是否重试？","错误提示",MessageInfoButton.RETRYNO) == ErrorMsgResult.Abort)
                               {
                                   throw ex;
                               }
                               else
                               {
                                   isRetry = true;
                               }            
                           }
                           isRetry = false;
                       }
                  }

                   //两根针取液完，依然剩余的，需在剩下的列里去继续取液
                   UnUseSlideTotal = UnusedSlide[Needle2];  
               }
////////////////////////////////////////////////////
                   //双针同时取液
               else  //该列有两个以上试剂瓶
               {
                   Position pos = new Position();
                   //移动X轴
                   int listNum = ReagentColList[0].Col;  //在第几列
                   pos.XPos = Location.RgColPos_X[listNum];
#if HARDWARE
                   MoveArmX(pos.XPos);
#endif
 
                   UsingReagentInformation[] YReagent = {null, null};

                   int serialNum = 0;  //试剂在list中的位置
                   int lineNum = 0; //试剂在设备中的位置
                   //Y
                   for (int NeedleNo = Needle2; NeedleNo  >= Needle1; NeedleNo -- )
                   {
                       if (listNum < Location.MaxColumnsBottle)//试剂管的列数
                       {
                           lineNum = ReagentColList[serialNum].Row;
                           pos.YPos[NeedleNo] = Location.RgRowBottlePos_Y[NeedleNo][lineNum];
                           YReagent[NeedleNo] = ReagentColList[serialNum];
                           serialNum++;
                           //                      pos.Y2Pos = (Position.Y2_ReagentZoneOffset + ReagentColList[colNo][rowNo].Row * Position.Height_ReagentTube);   //仪器的x轴 与 列宽对应
                       }
                       else   //试剂槽
                       {
                           lineNum = ReagentColList[serialNum].Row;
                           pos.YPos[NeedleNo] = Location.RgRowSlotPos_Y[NeedleNo][lineNum];
                           YReagent[NeedleNo] = ReagentColList[serialNum];
                           //                       pos.Y2Pos = Position.Y2_SlotOffset + ReagentColList[colNo][rowNo].Row * Position.Height_ReagentSlot;
                       }
                       // 针Y所对应的试剂            

                   }
                                    
  
#if HARDWARE
                   MoveArmY(pos.YPos);

#endif
                   //先吸一段空气，再吸液，多吸一小段 最后再吸一段空气，                  
                   RecordMsg("吸液前先吸一段空气");
                   int[] volume = {bigAirCapacity,bigAirCapacity};
                   bool [] useNeedle = {true ,true };
#if HARDWARE                   
                   SyncSuction(volume, useNeedle, Bottle); //
  
#endif
   
                   ReagentInformation[] aReagent = {
                                                       DataCollection.GetReagent("ReagentID", YReagent[Needle1].ReagentID) ,//获取试剂的完整信息
                                                       DataCollection.GetReagent("ReagentID", YReagent[Needle2].ReagentID)
                                                   };//获取试剂的完整信息


                   bool reagentIsElectrical = aReagent[0].IsElectrical;  //该试剂是否导电 ，如果不导电，则不做液面探测，直接到底

                   // 未使用的玻片总数
                   int SlideTotal = UnUseSlideTotal;
                   double[] zPos = new double[] { Location.Init_Position_Z, Location.Init_Position_Z };
 
                   bottle = DataCollection.GetSpecialBottle("BottleID", reagent.BottleID);

 //                  BottleState[] bottleStates = new BottleState[2] { new BottleState(BottleState.NOMAL), new BottleState(BottleState.NOMAL) };  
                   int[] usedSlide = new int[2];

                   if (reagentIsElectrical)
                   {
                       //探液
                       bool[] valideNeedle = new bool[] { true, true };
#if HARDWARE  
                       getLiquidLevel(valideNeedle, out zPos);
#endif
#if !HARDWARE  
                       zPos[0] = 40;
                       zPos[1] = 40;
#endif
                       //更新试剂瓶内试剂容量。(ml)（试剂的最大深度mm - Z轴位置mm）* 截面积(m2)  立方毫米 = 0.001ml
                       aReagent[Needle1].RealVolume = (armZ[Needle1].MaxDistance_mm - DevicePara.DieLiquidHight - zPos[Needle1]) * bottle.SurfaceArea / 1000.0;//
                       if (aReagent[Needle1].RealVolume < 0.0)                   
                           aReagent[Needle1].RealVolume = 0.0;
                       RecordMsg(aReagent[Needle1].ReagentName + " ("+aReagent[Needle1].ReagentID + ")剩余容量：" + aReagent[Needle1].RealVolume.ToString("F2"));
             
                       aReagent[Needle2].RealVolume = (armZ[Needle2].MaxDistance_mm - DevicePara.DieLiquidHight - zPos[Needle2]) * bottle.SurfaceArea / 1000.0;
                       if (aReagent[Needle2].RealVolume < 0.0)
                           aReagent[Needle2].RealVolume = 0.0;
                       RecordMsg(aReagent[Needle2].ReagentName + "("+aReagent[Needle2].ReagentID + ")剩余容量：" + aReagent[Needle2].RealVolume.ToString("F2"));
        //      
                       //z1计算下探位置
                       currentBottleStates[Needle1] = setZPos(vol, ref UnusedSlide[Needle1], ref zPos[Needle1], bottle);
                       zPos[Needle1] = zPos[Needle1] + 0.5;
                       //1#可加液玻片数量
                       usedSlide[Needle1] = SlideTotal - UnusedSlide[Needle2] - UnusedSlide[Needle1];

                       //z2
                       currentBottleStates[Needle2] = setZPos(vol, ref UnusedSlide[Needle2], ref zPos[Needle2], bottle);
                       zPos[Needle2] = zPos[Needle2] + 0.5;
                       //2#可加液玻片数量
                       usedSlide[Needle2] = SlideTotal - UnusedSlide[Needle2] - UnusedSlide[Needle1] - usedSlide[Needle1];

                       UnUseSlideTotal = UnusedSlide[Needle1] + UnusedSlide[Needle2];  //剩下的未使用的玻片数

                       //如果剩下的玻片小于10片，继续,否则重头再来一次,避免一个瓶子多，一个少时变成单针取样
                  //     bool Pump1IsNoFull =  ((SyringePump.MaxVolume-200)/vol - (usedSlide[Needle1])) > UnUseSlideTotal;
                  //      bool Pump2IsNoFull =  ((SyringePump.MaxVolume-200)/vol - (usedSlide[Needle2])) > UnUseSlideTotal;

                        if ((UnUseSlideTotal < 10) && (UnUseSlideTotal > 0))
                       {
                           for (int NeedleIndex = NeedleMax; NeedleIndex >= 0; NeedleIndex--)
                           {
                               bool PumpIsNoFull = ((SyringePump.MaxVolume - 300) / vol - (usedSlide[NeedleIndex])) > UnUseSlideTotal;
                               if ((currentBottleStates[NeedleIndex].State != BottleState.EMPTY) && PumpIsNoFull)
                               {
                                   UnusedSlide[NeedleIndex] = UnUseSlideTotal;
                                   currentBottleStates[NeedleIndex] = setZPos(vol, ref UnusedSlide[NeedleIndex], ref zPos[NeedleIndex], bottle);
                                   if (NeedleIndex == Needle1)
                                   {
                                       UnusedSlide[Needle2] = 0;
                                       usedSlide[Needle1] = SlideTotal - UnusedSlide[Needle1] - usedSlide[Needle2];
                                   }

                                   if (NeedleIndex == Needle2)
                                   {
                                       UnusedSlide[Needle2] = 0;
                                       usedSlide[Needle2] = SlideTotal - UnusedSlide[Needle2] - usedSlide[Needle1];
                                   }

                               }
                           }
                       }

                       UnUseSlideTotal = UnusedSlide[Needle1] + UnusedSlide[Needle2];  //s剩下的未使用的玻片数
                     
                   }
                   else  //不导电
                   {
                       UnUseSlideTotal = 0;
                       for (int NeedleIndex = NeedleMax; NeedleIndex >= 0; NeedleIndex--)
                       {
                           zPos[NeedleIndex] = armZ[NeedleIndex].MaxDistance_mm - 2; //最大深度 - 2mm                         
                           aReagent[NeedleIndex].RealVolume = 100;   //不导电的固定容量
                           if (UnusedSlide[NeedleIndex] > 5000 / vol)
                           {
                               UnusedSlide[NeedleIndex] = UnusedSlide[NeedleIndex] - 5000 / vol;
                               usedSlide[NeedleIndex] = 5000 / vol;
                           }
                           else
                           {
                               usedSlide[NeedleIndex] = UnusedSlide[NeedleIndex];
                               UnusedSlide[NeedleIndex] = 0;
                           }
                           UnUseSlideTotal = UnUseSlideTotal + UnusedSlide[NeedleIndex];
                       }
    //                   UnUseSlideTotal = 0;
                   }


                   // 在液面取液还是在瓶底取液
#if HARDWARE  
                   if (DevicePara.AbsorbLiquidMode == "Top")
                   {
                       MoveArmZ(useNeedle, zPos);
                   }
                   else
                   {
                       double[] zDepthMax = new double[] { armZ[0].MaxDistance_mm - 1.5, armZ[1].MaxDistance_mm - 1.5 };
                       MoveArmZ(useNeedle,zDepthMax);
                   }
#endif
                   for (int NeedleIndex = NeedleMax; NeedleIndex >= 0; NeedleIndex--)
                   {
                       //可加液玻片数量
                       PumpHaveSlide[NeedleIndex] = usedSlide[NeedleIndex];
                   //吸的液体总量 多吸
                       if (PumpHaveSlide[NeedleIndex] > 0)
                       {
                           volume[NeedleIndex] = (int)((double)usedSlide[NeedleIndex] * vol * GlobalValue.percent10 + appendLiquidCapacity);
                       }
                   }


                  //吸液
                   RecordMsg("1#2#泵吸液");
#if HARDWARE 
                     SyncSuction(volume, useNeedle, Bottle);
#endif
                   //记录容量
                     for (int NeedleIndex = NeedleMax; NeedleIndex >= 0; NeedleIndex--)
                     {
                         //                  double depth;
                         //                   armZ[NeedleIndex].getPosition(out depth);

                         String msg = armZ[NeedleIndex].ArmName + "吸液量" + volume[NeedleIndex].ToString("F2") + "(ul)";
                         msg = msg + "  当前位置：" + zPos[NeedleIndex].ToString("F2") + "(mm) \n\t";
                         aReagent[NeedleIndex].RealVolume = aReagent[NeedleIndex].RealVolume - syringePump[NeedleIndex].ActualVolume/1000.0;
                         msg = msg + aReagent[NeedleIndex].ReagentName + "(" + aReagent[NeedleIndex].ReagentID + ")当前剩余容量：" + aReagent[NeedleIndex].RealVolume.ToString("F2") + "（ml）";
                         RecordMsg(msg);
                     }
                   //保存吸液量，加液时使用
                   for (int NeedleIndex = NeedleMax; NeedleIndex >= 0; NeedleIndex--)
                   {
                           syringePump[NeedleIndex].ActualVolume = volume[NeedleIndex]; //


                       if ((currentBottleStates[NeedleIndex].State == BottleState.EMPTY) && aReagent[NeedleIndex].IsElectrical)
                       {
                           aReagent[NeedleIndex].IsEmpty = true;
                           ReagentEmpty(aReagent[NeedleIndex]);
                        }
                        else

                        {
                            aReagent[NeedleIndex].IsEmpty = false;
                        }
                           if (!DataCollection.UpdateReagentToDB(aReagent[NeedleIndex]))       //更新试剂库
                            {
                               DisPlayErrorMsg("更新试剂信息失败", "提示", MessageInfoButton.YES, MessageInfoBImage.WARNING);
                           }
          
                   


                       //更新剩余容量
    //                  saveReagentRealvol(syringePump[NeedleIndex].ActualVolume , aReagent[NeedleIndex]);   
                        saveReagentRealvol(0, aReagent[NeedleIndex]);  
                  }
               
                   //Z轴回零
                   RecordMsg("Z轴回零");
#if HARDWARE
                   MoveArmZFixPosition(Location.Init_Position_Z);
                   hardware.Sleep(1500);
#endif
                   //再吸一段空气
                   RecordMsg( "吸液后再吸一段空气");
#if HARDWARE               
                   volume[Needle1] = volume[Needle2] = smallAirCapacity;

                   SyncSuction(volume, useNeedle, Bottle);       
#endif
               }  
              return true;
          }


        /// <summary>
        /// 双针同时取两种不同的液体
        /// </summary>
        /// <param name="p"></param>
        /// <param name="vol"></param>
        /// <param name="RemainSlideTotal"></param>
        /// <param name="usingReagent"></param>
        /// <returns></returns>
        protected bool DoubleNeedlePickUpSimultantly(String strReagent, int vol, ref int UnUseSlideTotal, int appendLiquidCapacity = 40)
        {

            RecordMsg("开始双针取液。");
            Arm[] armZ = { hardware.ArmZ1, hardware.ArmZ2 };

            SyringePump[] syringePump = { hardware.syringePump[Needle1], hardware.syringePump[Needle2] };

            ObservableCollection<UsingReagentInformation> ReagentColList;
            //重试
            do
            {
                //查询符合条件的所有试剂列表
                ObservableCollection<UsingReagentInformation> findReagentList = null;
                findReagentList = FindReagent(strReagent);
                //查找在一列中的试剂
                ReagentColList = getReagentListInColumn(findReagentList, Location.MaxRowsBottle);

                if ((ReagentColList == null) || (ReagentColList.Count == 0))
                {
                    //               MessageBox.Show("在设备上无法找到可用的试剂。");
                    RecordMsg("在设备上无法找到可用的试剂——" + strReagent, 1, true, true, false);

                    //                     ErrorMsgResult.Abort == DisPlayErrorMsg("在设备上已无法找到可用的试剂,请添加试剂后重试。", "警告", MessageInfoButton.RETRYNO, MessageInfoBImage.WARNING))
                    DisPlayErrorMsg("在设备上已无法找到可用的该试剂类型，(" + strReagent + ")该类型试剂可能已用完！\r\n请添加试剂后重试。", "警告", MessageInfoButton.YES, MessageInfoBImage.OK);

                    //暂停事件
                    //                     PauseEvent();
                    CreatePauseEvent();
                    hardware.Sleep(100);
                    //                    Pause();
                }
            } while ((ReagentColList == null) || (ReagentColList.Count == 0));

            int amount = ReagentColList.Count;   //这一列有几个试剂瓶

            //将需要加液的片子均分,两根针的算法，如果多针，需设计通用算法
            int[] UnusedSlide = new int[2];

            if (UnUseSlideTotal % 2 == 0)
            {
                UnusedSlide[Needle1] = UnUseSlideTotal / 2;
                UnusedSlide[Needle2] = UnUseSlideTotal / 2;
            }
            else
            {
                UnusedSlide[Needle1] = UnUseSlideTotal / 2 + 1;
                UnusedSlide[Needle2] = UnUseSlideTotal / 2;
            }

            ReagentInformation reagent = DataCollection.GetReagent("ReagentID", ReagentColList[0].ReagentID);//获取试剂的完整信息

            /////////////////////////////////////////////////////////////////////////
            //这一列只有一个试剂瓶，并且不是试剂槽（试剂槽可以双针取）,先Y1取，再Y2取
            BottleInformation bottle = DataCollection.GetSpecialBottle("BottleID", reagent.BottleID);
            if ((amount == 1) && !(bottle.PlaceZone.Equals("试剂槽区")))
            {
                for (int NeedleIndex = Needle1; NeedleIndex <= NeedleMax; NeedleIndex++)
                {

                    if (NeedleIndex > Needle1)
                    {
                        UnusedSlide[NeedleIndex] = UnusedSlide[NeedleIndex] + UnusedSlide[Needle1];
                    }
                    //重试取液 Z1
                    bool isRetry = true;
                    while (isRetry)
                    {
                        try
                        {
                            SingleNeedlePickUp(NeedleIndex, strReagent, vol, ref UnusedSlide[NeedleIndex], appendLiquidCapacity);
                        }
                        catch (HardwareException ex)
                        {
                            String errorInfo = "错误信息：" + ex.Message + "\t\n错误源：" + ex.Source + "\t\n错误原因：" + ex.CauseofError;
                            if (DisPlayErrorMsg(" 取液失败.\t\n" + errorInfo + "\t\n是否重试？", "错误提示", MessageInfoButton.RETRYNO) == ErrorMsgResult.Abort)
                            {
                                throw ex;
                            }
                            else
                            {
                                isRetry = true;
                            }
                        }
                        isRetry = false;
                    }
                }

                //两根针取液完，依然剩余的，需在剩下的列里去继续取液
                UnUseSlideTotal = UnusedSlide[Needle2];
            }
            ////////////////////////////////////////////////////
            //双针同时取液
            else  //该列有两个以上试剂瓶
            {
                Position pos = new Position();
                //移动X轴
                int listNum = ReagentColList[0].Col;  //在第几列
                pos.XPos = Location.RgColPos_X[listNum];
#if HARDWARE
                   MoveArmX(pos.XPos);
#endif

                UsingReagentInformation[] YReagent = { null, null };

                int serialNum = 0;  //试剂在list中的位置
                int lineNum = 0; //试剂在设备中的位置
                //Y
                for (int NeedleNo = Needle2; NeedleNo >= Needle1; NeedleNo--)
                {
                    if (listNum < Location.MaxColumnsBottle)//试剂管的列数
                    {
                        lineNum = ReagentColList[serialNum].Row;
                        pos.YPos[NeedleNo] = Location.RgRowBottlePos_Y[NeedleNo][lineNum];
                        YReagent[NeedleNo] = ReagentColList[serialNum];
                        serialNum++;
                        //                      pos.Y2Pos = (Position.Y2_ReagentZoneOffset + ReagentColList[colNo][rowNo].Row * Position.Height_ReagentTube);   //仪器的x轴 与 列宽对应
                    }
                    else   //试剂槽
                    {
                        lineNum = ReagentColList[serialNum].Row;
                        pos.YPos[NeedleNo] = Location.RgRowSlotPos_Y[NeedleNo][lineNum];
                        YReagent[NeedleNo] = ReagentColList[serialNum];
                        //                       pos.Y2Pos = Position.Y2_SlotOffset + ReagentColList[colNo][rowNo].Row * Position.Height_ReagentSlot;
                    }
                    // 针Y所对应的试剂            

                }


#if HARDWARE
                   MoveArmY(pos.YPos);

#endif
                //先吸一段空气，再吸液，多吸一小段 最后再吸一段空气，                  
                RecordMsg("吸液前先吸一段空气");
                int[] volume = { bigAirCapacity, bigAirCapacity };
                bool[] useNeedle = { true, true };
#if HARDWARE                   
                   SyncSuction(volume, useNeedle, Bottle); //
  
#endif

                ReagentInformation[] aReagent = {
                                                       DataCollection.GetReagent("ReagentID", YReagent[Needle1].ReagentID) ,//获取试剂的完整信息
                                                       DataCollection.GetReagent("ReagentID", YReagent[Needle2].ReagentID)
                                                   };//获取试剂的完整信息


                bool reagentIsElectrical = aReagent[0].IsElectrical;  //该试剂是否导电 ，如果不导电，则不做液面探测，直接到底

                // 未使用的玻片总数
                int SlideTotal = UnUseSlideTotal;
                double[] zPos = new double[] { Location.Init_Position_Z, Location.Init_Position_Z };

                bottle = DataCollection.GetSpecialBottle("BottleID", reagent.BottleID);

                //                  BottleState[] bottleStates = new BottleState[2] { new BottleState(BottleState.NOMAL), new BottleState(BottleState.NOMAL) };  
                int[] usedSlide = new int[2];

                if (reagentIsElectrical)
                {
                    //探液
                    bool[] valideNeedle = new bool[] { true, true };
                    getLiquidLevel(valideNeedle, out zPos);

                    //更新试剂瓶内试剂容量。(ml)（试剂的最大深度mm - Z轴位置mm）* 截面积(m2)  立方毫米 = 0.001ml
                    aReagent[Needle1].RealVolume = (armZ[Needle1].MaxDistance_mm - DevicePara.DieLiquidHight - zPos[Needle1]) * bottle.SurfaceArea / 1000.0;//
                    if (aReagent[Needle1].RealVolume < 0.0)
                        aReagent[Needle1].RealVolume = 0.0;
                    RecordMsg(aReagent[Needle1].ReagentName + " (" + aReagent[Needle1].ReagentID + ")剩余容量：" + aReagent[Needle1].RealVolume.ToString("F2"));

                    aReagent[Needle2].RealVolume = (armZ[Needle2].MaxDistance_mm - DevicePara.DieLiquidHight - zPos[Needle2]) * bottle.SurfaceArea / 1000.0;
                    if (aReagent[Needle2].RealVolume < 0.0)
                        aReagent[Needle2].RealVolume = 0.0;
                    RecordMsg(aReagent[Needle2].ReagentName + "(" + aReagent[Needle2].ReagentID + ")剩余容量：" + aReagent[Needle2].RealVolume.ToString("F2"));
                    //      
                    //z1计算下探位置
                    currentBottleStates[Needle1] = setZPos(vol, ref UnusedSlide[Needle1], ref zPos[Needle1], bottle);
                    zPos[Needle1] = zPos[Needle1] + 0.5;
                    //1#可加液玻片数量
                    usedSlide[Needle1] = SlideTotal - UnusedSlide[Needle2] - UnusedSlide[Needle1];

                    //z2
                    currentBottleStates[Needle2] = setZPos(vol, ref UnusedSlide[Needle2], ref zPos[Needle2], bottle);
                    zPos[Needle2] = zPos[Needle2] + 0.5;
                    //2#可加液玻片数量
                    usedSlide[Needle2] = SlideTotal - UnusedSlide[Needle2] - UnusedSlide[Needle1] - usedSlide[Needle1];

                    UnUseSlideTotal = UnusedSlide[Needle1] + UnusedSlide[Needle2];  //剩下的未使用的玻片数

                    //如果剩下的玻片小于10片，继续,否则重头再来一次,避免一个瓶子多，一个少时变成单针取样
                    //     bool Pump1IsNoFull =  ((SyringePump.MaxVolume-200)/vol - (usedSlide[Needle1])) > UnUseSlideTotal;
                    //      bool Pump2IsNoFull =  ((SyringePump.MaxVolume-200)/vol - (usedSlide[Needle2])) > UnUseSlideTotal;

                    if ((UnUseSlideTotal < 10) && (UnUseSlideTotal > 0))
                    {
                        for (int NeedleIndex = NeedleMax; NeedleIndex >= 0; NeedleIndex--)
                        {
                            bool PumpIsNoFull = ((SyringePump.MaxVolume - 300) / vol - (usedSlide[NeedleIndex])) > UnUseSlideTotal;
                            if ((currentBottleStates[NeedleIndex].State != BottleState.EMPTY) && PumpIsNoFull)
                            {
                                UnusedSlide[NeedleIndex] = UnUseSlideTotal;
                                currentBottleStates[NeedleIndex] = setZPos(vol, ref UnusedSlide[NeedleIndex], ref zPos[NeedleIndex], bottle);
                                if (NeedleIndex == Needle1)
                                {
                                    UnusedSlide[Needle2] = 0;
                                    usedSlide[Needle1] = SlideTotal - UnusedSlide[Needle1] - usedSlide[Needle2];
                                }

                                if (NeedleIndex == Needle2)
                                {
                                    UnusedSlide[Needle2] = 0;
                                    usedSlide[Needle2] = SlideTotal - UnusedSlide[Needle2] - usedSlide[Needle1];
                                }

                            }
                        }
                    }

                    UnUseSlideTotal = UnusedSlide[Needle1] + UnusedSlide[Needle2];  //s剩下的未使用的玻片数

                }
                else  //不导电
                {
                    UnUseSlideTotal = 0;
                    for (int NeedleIndex = NeedleMax; NeedleIndex >= 0; NeedleIndex--)
                    {
                        zPos[NeedleIndex] = armZ[NeedleIndex].MaxDistance_mm - 2; //最大深度 - 2mm                         
                        aReagent[NeedleIndex].RealVolume = 100;   //不导电的固定容量
                        if (UnusedSlide[NeedleIndex] > 5000 / vol)
                        {
                            UnusedSlide[NeedleIndex] = UnusedSlide[NeedleIndex] - 5000 / vol;
                            usedSlide[NeedleIndex] = 5000 / vol;
                        }
                        else
                        {
                            usedSlide[NeedleIndex] = UnusedSlide[NeedleIndex];
                            UnusedSlide[NeedleIndex] = 0;
                        }
                        UnUseSlideTotal = UnUseSlideTotal + UnusedSlide[NeedleIndex];
                    }
                    //                   UnUseSlideTotal = 0;
                }


                // 在液面取液还是在瓶底取液
#if HARDWARE  
                   if (DevicePara.AbsorbLiquidMode == "Top")
                   {
                       MoveArmZ(useNeedle, zPos);
                   }
                   else
                   {
                       double[] zDepthMax = new double[] { armZ[0].MaxDistance_mm - 1.5, armZ[1].MaxDistance_mm - 1.5 };
                       MoveArmZ(useNeedle,zDepthMax);
                   }
#endif
                for (int NeedleIndex = NeedleMax; NeedleIndex >= 0; NeedleIndex--)
                {
                    //可加液玻片数量
                    PumpHaveSlide[NeedleIndex] = usedSlide[NeedleIndex];
                    //吸的液体总量 多吸
                    if (PumpHaveSlide[NeedleIndex] > 0)
                        volume[NeedleIndex] = (int)((double)usedSlide[NeedleIndex] * vol * GlobalValue.percent10 + appendLiquidCapacity);
                }


                //吸液
                RecordMsg("1#2#泵吸液");

                SyncSuction(volume, useNeedle, Bottle);

                //记录容量
                for (int NeedleIndex = NeedleMax; NeedleIndex >= 0; NeedleIndex--)
                {
                    //                  double depth;
                    //                   armZ[NeedleIndex].getPosition(out depth);

                    String msg = armZ[NeedleIndex].ArmName + "吸液量" + volume[NeedleIndex].ToString("F2") + "(ul)";
                    msg = msg + "  当前位置：" + zPos[NeedleIndex].ToString("F2") + "(mm) \n\t";
                    aReagent[NeedleIndex].RealVolume = aReagent[NeedleIndex].RealVolume - syringePump[NeedleIndex].ActualVolume / 1000.0;
                    msg = msg + aReagent[NeedleIndex].ReagentName + "(" + aReagent[NeedleIndex].ReagentID + ")当前剩余容量：" + aReagent[NeedleIndex].RealVolume.ToString("F2") + "（ml）";
                    RecordMsg(msg);
                }
                //保存吸液量，加液时使用
                for (int NeedleIndex = NeedleMax; NeedleIndex >= 0; NeedleIndex--)
                {
                    syringePump[NeedleIndex].ActualVolume = volume[NeedleIndex]; //


                    if ((currentBottleStates[NeedleIndex].State == BottleState.EMPTY) && aReagent[NeedleIndex].IsElectrical)
                    {
                        aReagent[NeedleIndex].IsEmpty = true;
                        ReagentEmpty(aReagent[NeedleIndex]);
                    }
                    else
                    {
                        aReagent[NeedleIndex].IsEmpty = false;
                    }
                    if (!DataCollection.UpdateReagentToDB(aReagent[NeedleIndex]))       //更新试剂库
                    {
                        DisPlayErrorMsg("更新试剂信息失败", "提示", MessageInfoButton.YES, MessageInfoBImage.WARNING);
                    }




                    //更新剩余容量
                    //                  saveReagentRealvol(syringePump[NeedleIndex].ActualVolume , aReagent[NeedleIndex]);   
                    saveReagentRealvol(0, aReagent[NeedleIndex]);
                }

                //Z轴回零
                RecordMsg("Z轴回零");
#if HARDWARE
                   MoveArmZFixPosition(Location.Init_Position_Z);
                   hardware.Sleep(1500);
#endif
                //再吸一段空气
                RecordMsg("吸液后再吸一段空气");
#if HARDWARE               
                   volume[Needle1] = volume[Needle2] = smallAirCapacity;

                   SyncSuction(volume, useNeedle, Bottle);       
#endif
            }
            return true;
        } 

        /// <summary>
        /// 通过探测液面自动更新容量
        /// </summary>
        /// <returns></returns>
        public bool TestReagentVolAuto()
        {
            RecordMsg("开始检测液面。");
            Arm[] armZ = { hardware.ArmZ1, hardware.ArmZ2 };


            ObservableCollection<UsingReagentInformation> usingReagentList = new ObservableCollection<UsingReagentInformation>();
            usingReagentList = DataCollection.GetAllUsingReagents();

            //查找在一列中的试剂
            ReagentMaxColumn = Location.MaxColumnsBottle;
            ObservableCollection<UsingReagentInformation>[] ReagentColList = new ObservableCollection<UsingReagentInformation>[ReagentMaxColumn]; //5列试剂
            for (int i = 0; i < ReagentColList.Count(); i++)
            {
                ReagentColList[i] = new ObservableCollection<UsingReagentInformation>();
            }

            ReSourceHardware hw = new ReSourceHardware();
            hw.GetHardwareHandle();

            //洗针
            RestStation();
            hardware.NeedleIsWashed = Shallow;
            StartWashNeedle(Shallow, 1);

            foreach (UsingReagentInformation ur in usingReagentList)
            {
                if (ur.Col < ReagentMaxColumn)
                    ReagentColList[ur.Col].Add(ur);                              //根据试剂的列号 将试剂放到不同的列表中
            }

            for (int index = 0; index < ReagentColList.Length; index++)
            {
                if (ReagentColList[index].Count == 0)
                    continue;

               int bottleTotal = ReagentColList[index].Count;
                UsingReagentInformation[] usingReagnt = new UsingReagentInformation[2];
                int serialNum = 0;
                double[] yPos = new double[2];
                double[] zPos = new double[] { Location.Init_Position_Z, Location.Init_Position_Z };
                //               do
                 
                
                while (serialNum < bottleTotal)
                {

                    double xPos = Location.RgColPos_X[index];
#if HARDWARE
                MoveArmX(xPos);


                    hardware.ArmY[Needle1].getPosition(out yPos[Needle1]);

                    hardware.ArmY[Needle2].getPosition(out yPos[Needle2]);
#endif
                    bool[] valideNeedle = new bool[] { false, false };

                    if ((GlobalValue.Select_Y2Needle) && (serialNum < bottleTotal))
                    {
                        usingReagnt[Needle2] = ReagentColList[index][serialNum++];
                        int lineNum = usingReagnt[Needle2].Row;
                        if (lineNum < (Location.MaxRowsBottle - 1))
                        {
                            yPos[Needle2] = Location.RgRowBottlePos_Y[Needle2][lineNum]; //
                            valideNeedle[Needle2] = true;
                        }
                        else
                        {
                            serialNum--;
                        }
                    }



                    if ((GlobalValue.Select_Y1Needle) && (serialNum < bottleTotal))
                    {
                        usingReagnt[Needle1] = ReagentColList[index][serialNum++];
                        int lineNum = usingReagnt[Needle1].Row;
                        yPos[Needle1] = Location.RgRowBottlePos_Y[Needle1][lineNum]; //
                        valideNeedle[Needle1] = true;
                    }

                    if (!valideNeedle[Needle1])
                    {
                        yPos[Needle1] = yPos[Needle2] + 2;
                    }

                    if (!valideNeedle[Needle2])
                    {
                        yPos[Needle2] = yPos[Needle1] - 2;
                    }

                    MoveArmY(yPos, false);



                    getLiquidLevel(valideNeedle, out zPos, true);
                   
                    ReagentInformation aReagent = new ReagentInformation();

                    for (int needleNo = Needle1; needleNo <= NeedleMax; needleNo++)
                    {
                        if (valideNeedle[needleNo])
                        {

                            aReagent = DataCollection.GetReagent("ReagentID", usingReagnt[needleNo].ReagentID);//获取试剂的完整信息

                            BottleInformation bottle = DataCollection.GetSpecialBottle("BottleID", aReagent.BottleID);

                            aReagent.RealVolume = (armZ[needleNo].MaxDistance_mm - DevicePara.DieLiquidHight - zPos[needleNo]) * bottle.SurfaceArea / 1000.0;//
                            //更新预计剩余量x-20210319
                            aReagent.Preconsumption = aReagent.RealVolume;
                            RecordMsg(aReagent.ReagentName + "（" + aReagent.ReagentID + "）剩余容量：" + aReagent.RealVolume.ToString("F2"));           

                            if (aReagent.RealVolume < 0.0)
                                aReagent.RealVolume = 0.0;

                            if (aReagent.RealVolume < 0.2) //小于200ul认为空
                            {
                                aReagent.IsEmpty = true;
                                ReagentEmpty(aReagent);
                            }
                            else   //否则非空
                            {
                                aReagent.IsEmpty = false;
                            }

                            //如果为一抗试剂或增效剂，使用容量大于总容量就为空
                            double ReagentConsumptionTotal = getReagentConsuption(aReagent);
                            if ((aReagent.CategoryID == 2) || (aReagent.CategoryID == 11))
                            {
                                if (ReagentConsumptionTotal > aReagent.TotalVolume)
                                {
                                    aReagent.IsEmpty = true;
                                    ReagentEmpty(aReagent);
                                }
                            }
                            
                            if (!DataCollection.UpdateReagentToDB(aReagent))
                            {
                                DisPlayErrorMsg("更新试剂信息失败", "提示", MessageInfoButton.YES, MessageInfoBImage.WARNING);
                            }
     //                       UpdataReagentDisplayState(aReagent);
                            UpdateReagent(aReagent);
                        }
                    }

                    RestStation();
                    hardware.NeedleIsWashed = Shallow;
                    StartWashNeedle(Shallow,1);

                }

            }
            hw.RealeseHardwareHandle();
            //探液
            return true;
        }

        protected void UpdataReagentDisplayState(ReagentInformation aReagent)
        {
            UpdateReagent(aReagent);
        }

        //
        protected int setWashNeedleHole(bool electric)
        {
            int hole;
            if ((DevicePara.AbsorbLiquidMode == "Top") && electric)
            {
                hole = Shallow;
            }
            else
            {

                hole = Deeper;
            }

            return hole;
        }

        /// <summary>
        /// 得到某试剂的总消耗量
        /// </summary>
        /// <param name="aReagent"></param>
        private  double getReagentConsuption(ReagentInformation aReagent)
        {
            string[] keyWord = new string[1] { "ReagentID" };
            object[] val = new object[1];
            val[0] = aReagent.ReagentID;
            ObservableCollection<ReagentConsumption> consumtionReagentList = new ObservableCollection<ReagentConsumption>();
            consumtionReagentList = DataCollection.GetSpecialReagentConsumption(keyWord, val);
            double consumtionTotal = 0;
            foreach (ReagentConsumption rc in consumtionReagentList)
            {
                consumtionTotal += rc.ConsumptionVolume;
            }
            return consumtionTotal;
        }

        /// <summary>
        /// 将试剂加入到指定位置
        /// </summary>
        /// <param name="strReagent"></param>
        /// <param name="totalvol">加入的总剂量</param>
        /// <returns></returns>
        protected bool AddReagentToCertainPlace(String strReagent, int totalvol, Position pos)
        {
            RecordMsg("开始取液。");
            Arm[] armZ = { hardware.ArmZ1, hardware.ArmZ2 };

            SyringePump[] syringePump = { hardware.syringePump[Needle1], hardware.syringePump[Needle2] };


            //未使用的玻片数
            int UnusedSlide = 200;  //虚拟200片
            int volPerSlide = totalvol / UnusedSlide;

   
            /////////////////////////////////////////////////////////////////////////
            while(UnusedSlide > 0)
            {
                //
                //取液 Z1
                bool isRetry = true;
                while ((isRetry) && (GlobalValue.Select_Y1Needle))
                {
                    try
                    {
                        SingleNeedlePickUp(Needle1, strReagent, volPerSlide, ref UnusedSlide);
                    }
                    catch (HardwareException ex)
                    {
                        String errorInfo = "错误信息：" + ex.Message + "\t\n错误源：" + ex.Source + "\t\n错误原因：" + ex.CauseofError;
                        if (DisPlayErrorMsg(" 取液失败.\t\n" + errorInfo + "\t\n是否重试？", "错误提示", MessageInfoButton.RETRYNO) == ErrorMsgResult.Abort)
                        {
                            throw ex;
                        }
                        else
                        {
                            isRetry = true;
                        }
                    }
                    isRetry = false;
                }

                //取液 Z2
                isRetry = true;
                 while ((isRetry) && (GlobalValue.Select_Y2Needle))
                 {
                     try
                     {
                         SingleNeedlePickUp(Needle2, strReagent, volPerSlide, ref UnusedSlide);
                     }
                     catch (HardwareException ex)
                     {
                         String errorInfo = "错误信息：" + ex.Message + "\t\n错误源：" + ex.Source + "\t\n错误原因：" + ex.CauseofError;
                         if (DisPlayErrorMsg(" 取液失败.\t\n" + errorInfo + "\t\n是否重试？", "错误提示", MessageInfoButton.RETRYNO) == ErrorMsgResult.Abort)
                         {
                             throw ex;
                         }
                         else
                         {
                             isRetry = true;
                         }
                       
                     }
                     isRetry = false;
                 }
                //计算未使用的玻片    
  //              UnUseSlideTotal = Y2UnusedSlide;
                //锅的第2列
 /*                  double xPos = Location.SlidePos_X[zoneNum][1]; 

                   double y1pos = Location.SlideRowPos_Y[0][Location.MaxRowsSlide-2];
                   double y2pos = Location.SlideRowPos_Y[1][Location.MaxRowsSlide-3];
                   double[] yPos = { y1pos, y2pos };

                   
                   double[] zPos = { 20, 20};
*/
                   KickOffAllReagent(pos);
            }
/*
            RecordMsg("返回洗站");
            RestStation();

            RecordMsg("洗针");
            StartWashNeedle(2);   //洗针
*/
            return true;
        }

        protected ObservableCollection<UsingReagentInformation> getReagentListInColumn(ObservableCollection<UsingReagentInformation> findReagentList,int MaxRow)
        {
    //        int listNum;

            //将所有查询到的试剂瓶放到不同的列表中。
            ObservableCollection<UsingReagentInformation>[] ReagentColList  = new ObservableCollection<UsingReagentInformation>[ReagentMaxColumn]; //5列试剂
            for (int i = 0; i < ReagentColList.Count(); i++)
            {
                ReagentColList[i] = new ObservableCollection<UsingReagentInformation>();
            }

            foreach (UsingReagentInformation ur in findReagentList)
            {
                ReagentColList[ur.Col].Add(ur);                              //根据试剂的列号 将试剂放到不同的列表中
            }

            //查找哪列有试剂瓶
   //         listNum = -1;
            int index;
            bool ReagentIsFound = false;
            for ( index = 0; index < ReagentMaxColumn; index++)  //遍历每列
            {
                if (ReagentColList[index].Count > 0)  //该列有所需的试剂瓶
                {
                    if ((ReagentColList[index].Count == 1) && (ReagentColList[index][0].Row >= MaxRow))
                    {
                        ReagentIsFound = false ;
                    }
                    else
                    {
                         ReagentIsFound = true;
                        break;
                    }
                }
            }

            if (ReagentIsFound)
                return ReagentColList[index];
            else
                return null;    
        }

        /// <summary>
        /// 同时吸液
        /// </summary>
        /// <param name="syringePump"></param>
        /// <returns></returns>
          protected bool SyncSuction(int[] vol, bool[] FindSlide,int Dir)
          {
              SYRINGE_STATUS  status;
              //                  syringeStatus = syringePump[First].Suction(bigAirCapacity);
              //控制阀切换
              if (FindSlide[Needle1])
              {
                  if(Dir  == Bottle)
                  {
                      hardware.syringePump[Needle1].OpenValveLeft();
                        
                  }
                  else
                  {
                      hardware.syringePump[Needle1].OpenValveRight();
                         
                  }
              }
              if (FindSlide[Needle2])
              {
                  if (Dir == Bottle)
                  {
                      hardware.syringePump[Needle2].OpenValveLeft();
                        
                  }
                  else
                  {
                      hardware.syringePump[Needle2].OpenValveRight();
                         
                  }
              }

              int returnPara;
              if (FindSlide[Needle1])
              {
                 
                   status = hardware.syringePump[Needle1].WaitExeResult(SyringePump.VALVEUnit, out returnPara);
                  if (SYRINGE_STATUS.SUCCESS != status)
                  {
                      throw new HardwareException("注射泵1工作过程中，阀故障。" + "故障代码:" + status, ErrorHardware.Syringe[Needle1], "故障原因：" + status);


 //                     DisPlayErrorMsg("注射泵1，阀故障。" + "故障代码:" + status);
 //                     return false;
                  }
              }

             if (FindSlide[Needle2])
             {
                 status = hardware.syringePump[Needle2].WaitExeResult(SyringePump.VALVEUnit, out returnPara);
                 if (SYRINGE_STATUS.SUCCESS != status)
                 {
                     throw new HardwareException("注射泵2工作过程中，阀故障。" + "故障代码:" + status, ErrorHardware.Syringe[Needle2], "故障原因：" + status);

  //                   DisPlayErrorMsg("注射泵2，阀故障。" + "故障代码:" + status);
  //                   return false;
                 }
             }

              //控制泵吸液
             if (FindSlide[Needle1])
             {
                 if (!hardware.syringePump[Needle1].Suction(vol[0]))
                     return false;
             }
             if (FindSlide[Needle2])
             {
                 if (!hardware.syringePump[Needle2].Suction(vol[1]))
                     return false;
             }
              //等待泵到位
             if (FindSlide[Needle1])
             {
                 status = hardware.syringePump[Needle1].WaitExeResult(SyringePump.PUMPUnit, out returnPara);
                 if (SYRINGE_STATUS.SUCCESS != status)
                 {
                     throw new HardwareException("注射泵1工作过程中，泵故障。" + "故障代码:" + status, ErrorHardware.Pump[Needle1], "故障原因：" + status);

   //                  DisPlayErrorMsg("注射泵1复位时，泵故障。" + "故障代码:" + status);
   //                  return false;
                 }
             }
             if (FindSlide[Needle2])
             {
                 status = hardware.syringePump[Needle2].WaitExeResult(SyringePump.PUMPUnit, out returnPara);
                 if (SYRINGE_STATUS.SUCCESS != status)
                 {
                     throw new HardwareException("注射泵2工作过程中，泵故障。" + "故障代码:" + status, ErrorHardware.Pump[Needle2], "故障原因：" + status);

 //                    DisPlayErrorMsg("注射泵2复位时，泵故障。" + "故障代码:" + status);
  //                   return false;
                 }
             }
              return true;
          }


/// <summary>
          /// 等待Z轴到位，
          /// 如果两根针都用，则FirstNeedle=needle1,LastNeedle = needle2,
          /// 如果只用第2根针，则FirstNeedle=needle1,LastNeedle = needle1,
          /// 如果只用第2根针，则FirstNeedle=needle2,LastNeedle = needle2,
/// </summary>
/// <param name="FirstNeedle">起始针，对应Z轴</param>
          /// <param name="LastNeedle">最后一根针，对应Z轴</param>
/// <param name="zPos"></param>
        private void WaitZPosArrived(int FirstNeedle,int LastNeedle, double[] zPos)
          {
              ARM_ERROR_CODE ArmError;
              ErrorMsgResult msgRt;
              bool[] isErro = new bool[2] { false, false };
              for (int NeedleIndex = LastNeedle; NeedleIndex >= FirstNeedle; NeedleIndex--)
              {
                  if (!hardware.ArmZ[NeedleIndex].ArrivedPos(out ArmError))
                  {
                      isErro[NeedleIndex] = true;
                      hardware.ArmZ[NeedleIndex].Stop();
                  }
              }
              //查询
              for (int NeedleIndex = NeedleMax; NeedleIndex >= 0; NeedleIndex--)
              {
                  while (isErro[NeedleIndex])
                  {
                      hardware.ArmZ[NeedleIndex].Stop();
   //                   hardware.ArmZ[NeedleIndex].ReleaseMotor();  //释放励磁
                      ReleaseMotor();
                      string errorInfo = hardware.ArmZ[NeedleIndex].ArmName + "运行错误.";
                      msgRt = DisPlayErrorMsg(errorInfo + "是否重试？", "机械臂运行错误", MessageInfoButton.RETRYNO);
                      if (msgRt == ErrorMsgResult.Retry)
                      {
                          hardware.ArmZ[NeedleIndex].SetPosition(Location.Init_Position_Z);
                          hardware.Sleep(800); //不能用WaitZPosArrive，可能造成递归死循环
                          hardware.ArmZ[NeedleIndex].SetPosition(zPos[NeedleIndex]);
                          isErro[NeedleIndex] = !hardware.ArmZ[NeedleIndex].ArrivedPos(out ArmError);
                      }
                      else
                      {
                          for (NeedleIndex = NeedleMax; NeedleIndex >= Needle1; NeedleIndex--) //所有Z轴复位
                              hardware.ArmZ[NeedleIndex].SetPosition(Location.Init_Position_Z);
                          hardware.Sleep(500);  ////是否可用WaitZPosArrive

                          throw new HardwareException(errorInfo, ErrorHardware.ArmZ[NeedleIndex], "错误原因：" + errorInfo);
                      }
                  }
              }
          }
/// <summary>
        /// 等待Y轴到位，
/// </summary>
/// <param name="yPos"></param>
        private void WaitYPosArrived(double[] yPos)
        {
            ARM_ERROR_CODE ArmError;
            ErrorMsgResult msgRt;
            bool[] isErro = new bool[] { false, false };
            for (int NeedleIndex = NeedleMax; NeedleIndex >= Needle1; NeedleIndex--)
            {
                if (!hardware.ArmY[NeedleIndex].ArrivedPos(out ArmError))
                {
                    isErro[NeedleIndex] = true;
                    hardware.ArmY[NeedleIndex].Stop();
                }
            }
            //查询
            for (int NeedleIndex = NeedleMax; NeedleIndex >= 0; NeedleIndex--)
            {
                if (isErro[NeedleIndex])
                {
                    hardware.ArmY[NeedleIndex].Stop();
 //                   hardware.ArmY[NeedleIndex].ReleaseMotor();  //释放励磁
                    ReleaseMotor();
                    string errorInfo = hardware.ArmY[NeedleIndex].ArmName + "运行错误.";
                    msgRt = DisPlayErrorMsg(errorInfo + "是否重试？", "机械臂运行错误", MessageInfoButton.RETRYNO);
                    if (msgRt == ErrorMsgResult.Retry)
                    {
                        MoveArmY(yPos,false);
                    }
                    else
                    {
                        throw new HardwareException(errorInfo, ErrorHardware.ArmY[NeedleIndex], "错误原因：" + errorInfo);
                    }
                }
            }
        }
        /// <summary>
        /// 等待X轴到位
        /// </summary>
        /// <param name="xPos"></param>
        private void WaitXPosArrived(double xPos)
        {
            ARM_ERROR_CODE ArmError;
            ErrorMsgResult msgRt;
 
                while (!hardware.ArmX.ArrivedPos(out ArmError))
                {
                    hardware.ArmX.Stop();
                    ReleaseMotor();
                    string errorInfo = hardware.ArmX.ArmName + "运行错误.";
                    msgRt = DisPlayErrorMsg(errorInfo + "是否重试？", "机械臂运行错误", MessageInfoButton.RETRYNO);
                    if (msgRt == ErrorMsgResult.Retry)
                    {
                        hardware.Sleep(500);
                        hardware.ArmX.SetPosition(xPos);
                    }
                    else
                    {
                        throw new HardwareException(errorInfo, ErrorHardware.ArmX, "错误原因：" + errorInfo);
                    }
                }
            }

        private void ReleaseMotor()
        {
            hardware.ArmX.ReleaseMotor();  //释放励磁
            hardware.Sleep(500);
            hardware.ArmY1.ReleaseMotor();  //释放励磁
            hardware.Sleep(500);
            hardware.ArmY2.ReleaseMotor();  //释放励磁
            hardware.Sleep(500);
            hardware.ArmZ1.ReleaseMotor();  //释放励磁
            hardware.Sleep(500);
            hardware.ArmZ2.ReleaseMotor();  //释放励磁
            hardware.Sleep(500);
        }

        /// <summary>
        /// 等待Z轴到位，
        /// 如果两根针都用，则FirstNeedle=needle1,LastNeedle = needle2,
        /// 如果只用第1根针，则FirstNeedle=needle1,LastNeedle = needle1,
        /// 如果只用第2根针，则FirstNeedle=needle2,LastNeedle = needle2,
        /// </summary>
        /// <param name="FirstNeedle">起始针，对应Z轴</param>
        /// <param name="LastNeedle">最后一根针，对应Z轴</param>
        /// <param name="zPos"></param>
        private void WaitDetectSuccess(int FirstNeedle, int LastNeedle)
        {
            ARM_ERROR_CODE ArmError;
 
            bool[] isError = new bool[] { false, false };
            for (int NeedleIndex = LastNeedle; NeedleIndex >= FirstNeedle; NeedleIndex--)
            {
                if (!hardware.ArmZ[NeedleIndex].ArrivedPos(out ArmError))
                {
                    isError[NeedleIndex] = true;
                    hardware.ArmZ[NeedleIndex].Stop();
                }
            }

            //二次探液
            for (int NeedleIndex = NeedleMax; NeedleIndex >= Needle1; NeedleIndex--)
            {

            }

            //查询           
            for (int NeedleIndex = NeedleMax; NeedleIndex >= Needle1; NeedleIndex--)
            {
                while (isError[NeedleIndex])
                {
                    hardware.ArmZ[NeedleIndex].SetPosition(Location.Init_Position_Z);
                    hardware.Sleep(500);  ////是否可用WaitZPosArrive
                    hardware.ArmZ[NeedleIndex].detectLiquidLevel();  //重试一次
                    isError[NeedleIndex] = !hardware.ArmZ[NeedleIndex].ArrivedPos(out ArmError);

                    if (ArmError == ARM_ERROR_CODE.NO_DETECTED)  //没探到，认为瓶子为空
                    {
                        MoveArmZ(NeedleIndex, hardware.ArmZ[NeedleIndex].MaxDistance_mm);
                        isError[NeedleIndex] = false;
                    }
                    
                    if (isError[NeedleIndex]) //如果有错误
                    {

                            hardware.ArmZ[NeedleIndex].Stop();
                            hardware.ArmZ[NeedleIndex].SetPosition(Location.Init_Position_Z);
                            hardware.Sleep(500);  ////是否可用WaitZPosArrive
                            string errorInfo = hardware.ArmZ[NeedleIndex].ArmName + "探液失败.";
  //                          msgRt = DisPlayErrorMsg(errorInfo + "是否重试？", "机械臂运行错误", MessageInfoButton.RETRYNO);
 //                           ErrorMsgResult result = DisPlayErrorMsg(errorInfo + "是否继续执行？\n\t（重试）重新探液 \n\t(继续)继续往下执行 \n\t （否）中断该规程 ", "机械臂运行错误", MessageInfoButton.RETRYIGNORENO, MessageInfoBImage.ERROR);
                            ErrorMsgResult result = DisPlayErrorMsg(errorInfo + "是否继续执行？\n\t（重试）重新探液 \n\t（否）中断该规程 ", "机械臂运行错误", MessageInfoButton.RETRYNO, MessageInfoBImage.ERROR);
            /*                if (ErrorMsgResult.Continue == result) //没探到，继续,将Z轴插入底部
                            {
                                MoveArmZ(NeedleIndex, Location.MaxPos_Z - 2);
                                isError[NeedleIndex] = false;
                            }
             */ 
                            if (result == ErrorMsgResult.Abort)
                            {
 //                               for (NeedleIndex = NeedleMax; NeedleIndex >= Needle1; NeedleIndex--) //所有Z轴复位

                                throw new HardwareException(errorInfo, ErrorHardware.ArmZ[NeedleIndex], "错误原因：" + errorInfo);
                            }
                    }
                }
            }
        }


        private void WaitDetectLiquidSuccess(bool[] validNeedle, out ARM_ERROR_CODE[] ArmError,int NoDetectPos)
        {
            //          ARM_ERROR_CODE ArmError;
            //          bool[] isError = new bool[] {false,false };

            ArmError = new ARM_ERROR_CODE[NeedleMax + 1];
            for (int NeedleIndex = NeedleMax; NeedleIndex >= Needle1; NeedleIndex--)
            {
                ArmError[NeedleIndex] = ARM_ERROR_CODE.DETECTED;

                if (validNeedle[NeedleIndex])
                {
                    bool retry = false;
                    do
                    {
  //                      hardware.ArmZ[NeedleIndex].ArrivedPos(out ArmError[NeedleIndex]);

                        if (!hardware.ArmZ[NeedleIndex].ArrivedPos(out ArmError[NeedleIndex]))  //探液不成功
                        {

//                            hardware.ArmZ[NeedleIndex].Stop();


                            hardware.ArmZ[NeedleIndex].SetPosition(Location.Init_Position_Z);
                            hardware.Sleep(1000);  ////是否可用WaitZPosArrive

                            if (ArmError[NeedleIndex] == ARM_ERROR_CODE.NO_DETECTED)  //没探到，认为瓶子为空
                            {
                                //                              MoveArmZ(NeedleIndex, hardware.ArmZ[NeedleIndex].MaxDistance_mm);
 //                               hardware.ArmZ[NeedleIndex].SetPosition(Location.Init_Position_Z);
                               if(NoDetectPos == 0)
                                  MoveArmZ(NeedleIndex, Location.Init_Position_Z);
                               else
                                   MoveArmZ(NeedleIndex, hardware.ArmZ[NeedleIndex].MaxDistance_mm);
                                                          
                            }
                            else  //其他错误
                            {

                                double CurrentPos = Location.Init_Position_Z;
                                hardware.ArmZ[NeedleIndex].getPosition(out CurrentPos);
                                //记录错误信息
                                ErrorRecord.writerecord("错误代码：" + ArmError[NeedleIndex] + "  当前z轴位置" + CurrentPos);
                                if (CurrentPos > (Location.Init_Position_Z + 2))
                                {
                                    hardware.ArmZ[NeedleIndex].Stop();
                                    ReleaseMotor();
                       //              MessageBox.Show("请检查取液针是否在试剂瓶中，如果在试剂瓶中，\n\r 请手动将针从试剂瓶中拔出后，继续执行。");
                                }

                                string errorInfo = hardware.ArmZ[NeedleIndex].ArmName + "探液失败.\n\r";
                                errorInfo = errorInfo  +  "请检查取液针是否在试剂瓶中，如果在试剂瓶中，\n\r 请手动将针从试剂瓶中拔出后，继续执行。";
                                ErrorMsgResult result = DisPlayErrorMsg(errorInfo + "是否继续执行？\n\t（重试）重新探液 \n\t（否）中断该规程 ", "机械臂运行错误", MessageInfoButton.RETRYNO, MessageInfoBImage.ERROR);
                                if (result == ErrorMsgResult.Abort)
                                {
                                    //                               for (NeedleIndex = NeedleMax; NeedleIndex >= Needle1; NeedleIndex--) //所有Z轴复位

                                    throw new HardwareException(errorInfo, ErrorHardware.ArmZ[NeedleIndex], "错误原因：" + errorInfo);
                                }
                                else
                                {

                                    hardware.ArmZ[NeedleIndex].getPosition(out CurrentPos);
                                    if (CurrentPos > (Location.Init_Position_Z + 2))
                                    {
                                        hardware.ArmZ[NeedleIndex].SetPosition(Location.Init_Position_Z);
                                        hardware.Sleep(1000);
                                    }
                                    hardware.ArmZ[NeedleIndex].detectLiquidLevel();  //重试一次
                                    retry = true;
                                }
                            }
                        }


                    } while (retry);
                }

            }
        }

          string[] armMsg = {"Z1轴","Z2轴","Z3轴"};
          string[] pumpMsg = { "1泵", "2泵" };
      //    private bool SingleNeedlePickUp(int NeedleNo, int vol, ref int UnUseSlideTotal, UsingReagentInformation YReagent)
  /// <summary>
  /// 单针取液
  /// </summary>
  /// <param name="NeedleNo">哪一根针</param>
  /// <param name="strReagent">哪个试剂</param>
  /// <param name="vol">单片玻片的加液量</param>
  /// <param name="UnUseSlideTotal">需加液的玻片数量</param>
  /// <param name="appendLiquidCapacity">多加的加液量</param>
  /// <returns></returns>
        protected bool SingleNeedlePickUp(int NeedleNo, String strReagent, int vol, ref int UnUseSlideTotal, int appendLiquidCapacity = 40)
          {
              if (UnUseSlideTotal < 1)
                  return true;
              Arm armZ = null;
              SyringePump syringePump = null;

              armZ = hardware.ArmZ[NeedleNo];
              syringePump = hardware.syringePump[NeedleNo];

                  //查询符合条件的所有试剂列表
              ObservableCollection<UsingReagentInformation> findReagentList = null;
              ObservableCollection<UsingReagentInformation> ReagentColList;
   
              //查找试剂
              do  //允许添加试剂后重新查找
              {

                  findReagentList = FindReagent(strReagent);   //所有符合要求的试剂，后期是否可以考虑用类表示同一种试剂（即试剂名称+克隆号+规格），二不是这种连接    
                  if ((findReagentList == null) || (findReagentList.Count == 0))
                  {
                      RecordMsg("在设备上无法找到可用的试剂——" + strReagent, 1, true, true, false);

                      //                     ErrorMsgResult.Abort == DisPlayErrorMsg("在设备上已无法找到可用的试剂,请添加试剂后重试。", "警告", MessageInfoButton.RETRYNO, MessageInfoBImage.WARNING))
                      DisPlayErrorMsg("在设备上已无法找到可用的该试剂类型，(" + strReagent + ")该类型试剂可能已用完！\r\n请添加试剂后重试。", "警告", MessageInfoButton.YES, MessageInfoBImage.OK);

                    //暂停事件

                      CreatePauseEvent();
                      hardware.Sleep(100);
   //                   Pause();
                  }
                  int MaxRow = Location.MaxRowsBottle;
                  if (NeedleNo == Needle2)
                      MaxRow = Location.MaxRowsBottle - 1;

                  ReagentColList = getReagentListInColumn(findReagentList,MaxRow);  //取得第一个符合要求的列

              } while ((findReagentList == null) || (findReagentList.Count == 0));

              //Z2无法访问最后一个试剂瓶
              if (ReagentColList == null)
              {
                  MessageBox.Show("虽然设备上绑定有试剂"+ strReagent + ",\n\r但2#针无法访问到该位置的试剂");
                  return true;
              }

              Position pos = new Position();
              //         int SlideCounter = 0; //记录实际的洗液量能满足多少片玻片

              //移动X轴
//              pos.XPos = (Position.X_ReagentZoneOffset + colNo * Position.Width_ReagentTube);   //仪器的X轴 与 列宽对应
              int listNum = ReagentColList[0].Col;//找到的第1 个试剂在第几列
              pos.XPos = Location.RgColPos_X[listNum];
#if HARDWARE
              MoveArmX(pos.XPos);
                 
#endif
  
              int serialNum = 0;  //所选试剂列表的第几个
              UsingReagentInformation selectReagent = ReagentColList[serialNum];
              /////////////////单针，要不选1，要不选2
   
                  if (listNum < Location.MaxColumnsBottle)//
                  {
                      int lineNum = selectReagent.Row;  //第几行
                        pos.YPos[NeedleNo] = Location.RgRowBottlePos_Y[NeedleNo][lineNum];  //试剂瓶的位置坐标
                  }
                  else  //试剂槽
                  {
  //                    pos.Y2Pos = Position.Y2_SlotOffset + ReagentColList[colNo][rowNo].Row * Position.Height_ReagentSlot;
                      int lineNum = selectReagent.Row;
                      pos.YPos[NeedleNo] = Location.RgRowSlotPos_Y[NeedleNo][lineNum];      //试剂槽的位置坐标
                  }
                  //                       YFindReagent[1] = true;
 
             

#if HARDWARE
              double yPos =  pos.YPos[NeedleNo];
              MoveArmY(NeedleNo, yPos);

#endif
              //先吸一段空气，再吸液，多吸一小段 最后再吸一段空气，
              //hardware.PumpFirst.Suction(2000); //吸一段空气,还要多吸10ul液体
              RecordMsg(syringePump.PumpAddr + "#泵吸液前先吸一段空气");
#if HARDWARE
              syringePump.SuctionW(bigAirCapacity);    //阻塞模式        

              Thread.Sleep(100);
#endif

              ReagentInformation reagent = DataCollection.GetReagent("ReagentID", selectReagent.ReagentID);//获取试剂的完整信息

      
               bool reagentIsElectrical = reagent.IsElectrical;  //该试剂是否导电 ，如果不导电，则不做液面探测，直接到底

  //            Zmsg = armZ.ArmName;
               int SlideTotal = UnUseSlideTotal;
               double[] zPos = new double[] { Location.Init_Position_Z, Location.Init_Position_Z };
               BottleInformation bottle = DataCollection.GetSpecialBottle("BottleID", reagent.BottleID);
    
 //              CurrentBottleState = new BottleState(BottleState.NOMAL);
               if (reagentIsElectrical)
               {
                   //探液
                   bool[] valideNeedle = new bool[]{false,false};
                   valideNeedle[NeedleNo] = true;
#if HARDWARE
                   getLiquidLevel(valideNeedle, out zPos);
#endif
#if !HARDWARE
                   zPos[0] = 40;
                   zPos[1] = 41;
#endif
                   //更新试剂瓶内试剂容量。（试剂的最大深度 - Z轴位置）* 截面积
                   reagent.RealVolume = (armZ.MaxDistance_mm - DevicePara.DieLiquidHight - zPos[NeedleNo]) * bottle.SurfaceArea / 1000;
                   if (reagent.RealVolume < 0.0)
                       reagent.RealVolume = 0.0;
                   RecordMsg(reagent.ReagentName +"（"+ reagent.ReagentID + "）剩余容量：" + reagent.RealVolume.ToString("F2"));           
                   //下探 获取Z轴的深度
                   currentBottleStates[NeedleNo] = setZPos(vol, ref UnUseSlideTotal, ref zPos[NeedleNo], bottle);
 
               }
               else  //不导电
               {
                   zPos[NeedleNo] = armZ.MaxDistance_pls / armZ.Scale - 2; //最大深度 - 2mm

                   if (SlideTotal > 5000 / vol)
                   {
                       UnUseSlideTotal = SlideTotal - 5000 / vol;
                   }
                   else
                   {
                       UnUseSlideTotal = 0;
                   }
                   reagent.RealVolume = 100;//如果是不导电的，则固定100ml
               }

               //计算是否试剂够加所有的玻片
              int usedSlideTotal = SlideTotal - UnUseSlideTotal;

              //
#if HARDWARE
              if (DevicePara.AbsorbLiquidMode == "Top")
              {
                  zPos[NeedleNo] = zPos[NeedleNo] + 0.5;
                  MoveArmZ(NeedleNo, zPos[NeedleNo]);
              }
              else
              {
                  zPos[NeedleNo] = armZ.MaxDistance_mm - 1.5;
                  MoveArmZ(NeedleNo, zPos[NeedleNo]);
              }
#endif

              RecordMsg(syringePump.PumpAddr + "#泵吸液");
    //          pump.ActualVolume = usedSlideTotal * vol + appendLiquidCapacity;
              PumpHaveSlide[NeedleNo] = usedSlideTotal;
              if (PumpHaveSlide[NeedleNo] > 0)
              {
                  syringePump.ActualVolume = (int)((double)usedSlideTotal * vol * GlobalValue.percent10 + appendLiquidCapacity);
                  //吸液
#if HARDWARE
                  AbsorbReagent(syringePump);
#endif
#if !HARDWARE
                  RecordMsg(syringePump.PumpAddr + "#泵吸液完成吸液（test）");
#endif

              }

              //记录当前位置
              String msg = armZ.ArmName + "吸液量" + syringePump.ActualVolume.ToString("F2") + "(ul)";
              msg = msg + "取液位置：" + zPos[NeedleNo].ToString("F2") + "(mm) \n\t";
              reagent.RealVolume = reagent.RealVolume - syringePump.ActualVolume/1000.0;
              msg = msg + "   " + reagent.ReagentName + " (" + reagent.ReagentID + ")当前剩余容量：" + reagent.RealVolume.ToString("F2") + "（ml）";
              RecordMsg(msg);

              //如果试剂瓶空，标记为空,且位于试剂瓶区时
 //             if ((currentBottleStates[NeedleNo].State == BottleState.EMPTY) && (!reagent.IsEmpty) && (bottle.PlaceZone == PlaceZone.BOTTLE))
              if ((currentBottleStates[NeedleNo].State == BottleState.EMPTY) && reagent.IsElectrical)
              {
                  reagent.IsEmpty = true;
                  ReagentEmpty(reagent);
              }
              else
              {
                  reagent.IsEmpty = false;
   //               UpdateReagent(reagent);
              }
                  if (!DataCollection.UpdateReagentToDB(reagent))       //更新试剂库
                  {
                      DisPlayErrorMsg("更新试剂信息失败", "提示", MessageInfoButton.YES, MessageInfoBImage.WARNING);
                  }

 

              //更新试剂剩余容量              
              //            saveReagentRealvol(syringePump.ActualVolume, reagent);
              saveReagentRealvol(0, reagent);
              
              //Z轴回零
              RecordMsg(armZ.ArmName + "回位");
#if HARDWARE
              MoveArmZFixPosition(Location.Init_Position_Z);

#endif
              hardware.Sleep(1500);//等待滴液
              //再吸一段空气
              RecordMsg(syringePump.PumpAddr + "#泵最后再吸一段空气");
#if HARDWARE
              syringePump.SuctionW(smallAirCapacity);
#endif
             return true;
  
          }

        /// <summary>
        /// 产生暂停事件
        /// </summary>
        public void CreatePauseEvent()
        {
            GlobalValue.RunningStatus = RUNNINGSTATUS.PAUSE;
            PauseEvent();
        }

        //转发消息事件
        public void TransMessageEvent(String strMsg)
        {
            MessageOccured(strMsg);
        }
/// <summary>
/// 暂停
/// </summary>
        protected void Pause()
        {
            WorkStatus threadStatus = WorkStatus.getInstance();
            bool isHave = false;  //是否占有硬件资源
            threadStatus.Status[zoneNum] = WorkStatus.Pause;
            if (hardware.isHaveHardWare())
            {
                isHave = true;
                hardware.RealeseHardwareHandle();
            }

            int status;
            do
            {
                status = getWorkStatus();
                Thread.Sleep(100);
                    
            } while (status == WorkStatus.Pause);

            //等待获取权限
            if (isHave)
            {
                 do
                {
                    Thread.Sleep(100);

                } while (hardware.GetHardwareHandle());      
            }

        }

        protected int getWorkStatus()
        {
            WorkStatus WorkStatus = WorkStatus.getInstance();
            return WorkStatus.Status[zoneNum];
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="NeedleNo"></param>
        /// <param name="zoneNum"></param>
        /// <param name="vol"></param>
        /// <param name="numUnuseSlide"></param>
        /// <returns></returns>
          protected bool PickUpDAB(int NeedleNo,  int vol, ref int numUnuseSlide)
          {
              if (numUnuseSlide < 1)
                  return true;

              SyringePump syringePump = hardware.syringePump[NeedleNo];
              Arm armZ = hardware.ArmZ[NeedleNo];

              Position pos = new Position();
 
              //移动X轴
              if(zoneNum%2 == 0)
                  pos.XPos = Location.DABStation13_X;   //仪器的X轴 与 列宽对应
              else
                  pos.XPos = Location.DABStation24_X;   //仪器的X轴 与 列宽对应

#if HARDWARE
              MoveArmX(pos.XPos);
  
#endif

              UsingReagentInformation YReagent = null;  //y1轴对应试剂瓶

              //              int amount = ReagentColList[colNo].Count;   //这一列有几个试剂瓶

              //读取Y值
              double ypos = Location.DABStation_Y[NeedleNo][zoneNum];
 //             pos.YPos[Needle2] = Location.DABStation_Y[Needle2][zoneNum]; 

#if HARDWARE
              MoveArmY(NeedleNo, ypos);

#endif
              //先吸一段空气，再吸液，多吸一小段 最后再吸一段空气，

              //hardware.PumpFirst.Suction(2000); //吸一段空气,还要多吸10ul液体
              RecordMsg(syringePump.PumpAddr + "#泵吸液前先吸一段空气");
#if HARDWARE
              syringePump.SuctionW(bigAirCapacity);   //阻塞模式        
  
              Thread.Sleep(100);
#endif

  //            ReagentInformation reagent = DataCollection.GetReagent("ReagentID", YReagent.ReagentID);//获取试剂的完整信息

              //探液并下探
              double[] LevelPos = new double[2];
              LevelPos[Needle1] = 30;//Location.DABStation_Z[Needle1];
              LevelPos[Needle2] = 30; //Location.DABStation_Z[Needle2];
              bool[] useNeedle = {false,false};
              useNeedle[NeedleNo] = true;  //
     //以下屏蔽
     /*         if (!DetectLiquidLevel( LevelPos, useNeedle))
                  return false;
              //得到当前位置
              double currenLevel;
              if (!armZ.getPosition(out currenLevel))
                  return false;
    */
              RecordMsg(syringePump.PumpAddr + "#泵吸液");
              //          pump.ActualVolume = usedSlideTotal * vol + appendLiquidCapacity;
              double totalVolumn = numUnuseSlide * vol;
              int usedSlide;
              int maxSlide = 4500 / vol;
              double depth;
              if (numUnuseSlide > maxSlide)
              {
                  usedSlide = maxSlide;
                  syringePump.ActualVolume = (int)((double)usedSlide * vol * GlobalValue.percent10 + 60);
                  depth = Location.DABStation_Z[NeedleNo];
                 
              }
              else
              {
                  usedSlide = numUnuseSlide;
                  syringePump.ActualVolume = (int)((double)usedSlide * vol * GlobalValue.percent10 + 60);
   //               depth = currenLevel + syringePump.ActualVolume / DevicePara.AreaDAB;
                  depth = Location.DABStation_Z[NeedleNo];

              }
              numUnuseSlide = numUnuseSlide - usedSlide;

              RecordMsg(armZ.ArmName + "移动：" + depth.ToString("F2") + "mm");
#if HARDWARE
              armZ.SetPositionW(depth);
 
#endif

#if HARDWARE
              //吸液
              AbsorbReagent(syringePump);
#endif
              PumpHaveSlide[NeedleNo] = usedSlide;

              RecordMsg(armZ.ArmName + "当前位置：" + depth.ToString("F2") + "mm");
              //Z轴回零
              RecordMsg(armZ.ArmName + "回位");
#if HARDWARE
              MoveArmZFixPosition(Location.Init_Position_Z);

#endif

              //再吸一段空气
              RecordMsg(syringePump.PumpAddr + "#泵最后再吸一段空气");
#if HARDWARE
              syringePump.SuctionW(smallAirCapacity);
#endif
              return true;

          }
       
        /*
          private bool Suction(SyringePump syringePump,int volume)
          {
              if (!syringePump.Suction(volume))
              {
                  return false;
              }
              return true;
          }
 */ 
/// <summary>
/// 硬件开始吸液
/// </summary>
/// <param name="syringePump"></param>
/// <param name="reagent"></param>
/// <returns></returns>
          private bool AbsorbReagent(SyringePump syringePump)
          {
              RecordMsg(syringePump.PumpAddr + "#泵开始吸液" );
 #if HARDWARE
              if (syringePump.ActualVolume > 0)
                  syringePump.SuctionW(syringePump.ActualVolume);
                
#endif
              //等待时间 泵停止之后 液体依然在管路中流动
           int waitTime =  syringePump.ActualVolume + 1000;
             hardware.Sleep(waitTime);
             return true;
              //更新试剂容量信息x-20190801 
          }
        /// <summary>
        /// 获取Z轴在试剂瓶中的位置，并计算可加液的玻片数量
        /// </summary>
        /// <param name="vol">每片需加的试剂量</param>
        /// <param name="unuseSlideTotal">还未被使用的玻片数量</param>
          /// <param name="zPos">Z轴的位置</param>
        /// <param name="reagent">试剂瓶信息</param>
        /// <returns>状态，0表示正常，1表示已到最大吸液量，2表示已到底部</returns>
          private BottleState setZPos(int vol, ref int unuseSlideTotal, ref double zPos, BottleInformation bottle)
          {
                Arm armZ = hardware.ArmZ[0];   //  需两根针的高度相同

                int SlideCounter = 0;

                BottleState state = new BottleState(BottleState.NOMAL);

                 double area = bottle.SurfaceArea;

                  while ((SlideCounter < unuseSlideTotal))
                  {
                      if ((SlideCounter * vol) > (SyringePump.MaxVolume-300))  //最大吸液量
                      {
                          state.State = BottleState.NOMAL;
                          break;
                      }
                       zPos = zPos + (vol / area); //设置Z
                      //液面最大深度-----MaxDepth 也即Z轴的行程(pix)
                       double MaxDepth = armZ.MaxDistance_mm;  //armZ.MaxDistance_pls / armZ.Scale;  //Z2与Z1相同

                      if (bottle.PlaceZone == PlaceZone.BOTTLE)  //试剂瓶
                      {
                          MaxDepth = MaxDepth - DevicePara.DieLiquidHight;
                      }

                      if (bottle.PlaceZone == PlaceZone.SLOT) //试剂槽
                      {
                          MaxDepth = MaxDepth - 5;
                      }

                      if (zPos > (MaxDepth - bottle.BottleHeight / 2.0))   //1/2
                          state.State = BottleState.HALF;

                      if (zPos > (MaxDepth - bottle.BottleHeight / 4))   //76.5为瓶高度，1/4
                          state.State = BottleState.QUATER;

 

                      if (zPos > MaxDepth)        //已到底部
                      {
                          //已达到试剂底部，无法取样，把其标为空
 //                         RecordMsg("已到底部，该瓶已无法继续取样！");
   //                       pump.ActualVolume = (int)vol * (SlideCounter - 1); //泵需要吸多少液
                          zPos = zPos - (vol / area); //设置Z
                          state.State = BottleState.EMPTY;
                          break;
                      }
                      else
                      {
                         SlideCounter++;
                      }

                  }

                  unuseSlideTotal = unuseSlideTotal - SlideCounter;

//                  state.State = BottleState.EMPTY;
                  return state;
          }

          /// <summary>
          /// 获取液面位置
          /// </summary>
          /// <param name="validNeedle"></param>
          /// <param name="zPos"></param>
          /// <returns></returns>
          private bool getLiquidLevel(bool[] validNeedle, out double[] zPos,bool isSecondDetect = true)
          {

              ARM_ERROR_CODE[] ArmError;
              if (validNeedle[Needle1])
              {
                  bool[] SingleNeedle = new bool[] { true, false };
                  DetectLiquidLevelSimultanelty(SingleNeedle);
                  WaitDetectLiquidSuccess(SingleNeedle, out ArmError, 0);
              }

              if (validNeedle[Needle2])
              {
                  bool[] SingleNeedle = new bool[] { false, true };
                  DetectLiquidLevelSimultanelty(SingleNeedle);
                  WaitDetectLiquidSuccess(SingleNeedle, out ArmError, 0);
              }


              double[] zPosFirst = new double[2] { Location.Init_Position_Z, Location.Init_Position_Z };
              double[] zPosSecond = new double[2] { Location.Init_Position_Z, Location.Init_Position_Z };
              for (int NeedleIndex = NeedleMax; NeedleIndex >= 0; NeedleIndex--)
              {
                  hardware.ArmZ[NeedleIndex].getPosition(out zPosFirst[NeedleIndex]);
                  msg = hardware.ArmZ[NeedleIndex].ArmName + "第1次探液，液面位置:" + zPosFirst[NeedleIndex].ToString("F2") + "mm";
                  RecordMsg(msg);
                  if (validNeedle[NeedleIndex])
                  {
                      MoveArmZ(NeedleIndex, Location.Init_Position_Z);
                  }
                  /*
                  if (zPosFirst[NeedleIndex] > (Location.Init_Position_Z + 2))
                  {
                      MoveArmZ(NeedleIndex, zPosFirst[NeedleIndex] - 30);
                  }
*/
              }

              if (isSecondDetect)
              {
                  //二次探液
                  if (validNeedle[Needle1])
                  {
                      bool[] SingleNeedle = new bool[] { true, false };
                      DetectLiquidLevelSimultanelty(SingleNeedle);
                      WaitDetectLiquidSuccess(SingleNeedle, out ArmError, 1);
                  }

                  if (validNeedle[Needle2])
                  {
                      bool[] SingleNeedle = new bool[] { false, true };
                      DetectLiquidLevelSimultanelty(SingleNeedle);
                      WaitDetectLiquidSuccess(SingleNeedle, out ArmError, 1);
                  }


                  for (int NeedleIndex = NeedleMax; NeedleIndex >= 0; NeedleIndex--)
                  {
                      hardware.ArmZ[NeedleIndex].getPosition(out zPosSecond[NeedleIndex]);
                      msg = hardware.ArmZ[NeedleIndex].ArmName + "第2次探液，液面位置:" + zPosSecond[NeedleIndex].ToString("F2") + "mm";
                      RecordMsg(msg);
                  }
              }
              //获取最大值
              zPos = new double[2] { Location.Init_Position_Z, Location.Init_Position_Z };
              for (int NeedleIndex = NeedleMax; NeedleIndex >= 0; NeedleIndex--)
              {
                  Arm armZ = hardware.ArmZ[NeedleIndex];
                  //获取针的位置
#if HARDWARE
                  //               armZ.getPosition(out zPos[NeedleIndex]);
                  zPos[NeedleIndex] = Math.Max(zPosFirst[NeedleIndex], zPosSecond[NeedleIndex]);

                  zPos[NeedleIndex] = zPos[NeedleIndex] + 0.1;
#endif
                  msg = armZ.ArmName + "液面位置:" + zPos[NeedleIndex].ToString("F2") + "mm";
                  RecordMsg(msg);
              }
              return true;
          }

          private void DetectLiquidLevelSimultanelty(bool[] validNeedle)
          {
              for (int NeedleIndex = NeedleMax; NeedleIndex >= 0; NeedleIndex--)
              {
                  if (validNeedle[NeedleIndex])     //液面探测
                  {
                      RecordMsg(hardware.ArmZ[NeedleIndex].ArmName + "开始探液");
                      //           double LevelPos = getBottleLevel(reagent) - 10;  //获取瓶子中液体的液面位置
                      double LevelPos = 25.66;   //高速位置
                      hardware.ArmZ[NeedleIndex].detectLiquidLevel(LevelPos);//注意设置面探测的最大距离，需通过调试软件设置
                  }
              }
              Thread.Sleep(2000);
          }

        /// <summary>
        /// 保存试剂剩余容量
        /// </summary>
        /// <param name="vol"></param>
        /// <param name="reagent"></param>
        /// <returns></returns>
          private double saveReagentRealvol(double vol, ReagentInformation reagent)
          {
              double ConsumptionVol = vol / 1000.0;//微升转ml
  
              //更新当前当天消耗量  
              if (!DataCollection.UpdateReagentConsumption(reagent.ReagentID, (float)ConsumptionVol))
              {
                  String volInfo = reagent.ReagentName + "(" + reagent.ReagentID + "),更新试剂用量信息失败,剩余容量：" + reagent.RealVolume + "   消耗容量：" + ConsumptionVol;
  //                DailyRecord.writerecord(volInfo);
  //                RecordMsg(msg);
                  DisPlayErrorMsg(volInfo, "提示", MessageInfoButton.YES, MessageInfoBImage.WARNING);
              }

              //更新试剂剩余量
              reagent.RealVolume = reagent.RealVolume - ConsumptionVol;

              double ReagentConsumptionTotal = getReagentConsuption(reagent);
              if ((reagent.CategoryID == 2) || (reagent.CategoryID == 11))
              {
                  if (ReagentConsumptionTotal > reagent.TotalVolume)  //如果消耗量大于总容量，则为空
                  {
                      reagent.IsEmpty = true;
                  }
              }

              if(!DataCollection.UpdateReagentToDB(reagent))
              {
                  DisPlayErrorMsg("更新试剂信息失败", "提示", MessageInfoButton.YES, MessageInfoBImage.WARNING);
              }

              
         

              return reagent.RealVolume;
          }
        //获取瓶子剩余的液面高度
          private double getBottleLevel(ReagentInformation reagent)
          {
              double depth;
              double area = DataCollection.GetBottleAreaFromDB("BottleID", reagent.BottleID);
              depth = Location.BottleMaxDepth - reagent.RealVolume * 1000 / area;
              return depth;
          }

        /// <summary>
        /// 释放励磁
        /// </summary>
        /// <param name="armNum"></param>
        /// <returns></returns>
         public void ReleaseMotor(int armNum)
        {
            ReSourceHardware.ArmGroup[armNum].ReleaseMotor();
     
        }

 
        /// <summary>
        /// 同时移动Y1Y2 轴
        /// </summary>
        /// <param name="y1Pos">Y轴</param>
        /// <returns>是否成功</returns>
         public bool MoveArmY(double[] yPos, bool checkedZ = true)
        {
            //
            //如果从左往右则先移动y1,再移动y2，
            //从右往左，先移动y2,再移动y1，

            //移动X轴之前，z轴归零附近，确保Z轴不会与其他部件干涉即可
            double y1Pos = yPos[0];
            double y2Pos = yPos[1];
            System.Diagnostics.Debug.Assert(y1Pos > y2Pos);
            if (y1Pos < y2Pos)
             {
                 MessageBox.Show("错误 : y1Pos < y2Pos");
             }
            RecordMsg("开始移动Y轴");
   
            if (checkedZ)
            {
                double posZ1;
                double posZ2;
                hardware.ArmZ[Needle1].getPosition(out posZ1);

                hardware.ArmZ[Needle2].getPosition(out posZ2);


                if ((posZ1 > (Location.Init_Position_Z + 1))
                    || (posZ2 > (Location.Init_Position_Z + 1)))
                {

                    double[] zPos = new double[] { Location.Init_Position_Z, Location.Init_Position_Z };
                    MoveArmZFixPosition(Location.Init_Position_Z);
                    WaitZPosArrived(Needle1, Needle2, zPos);

                }
            }
            //如果目标位置 大于当前位置，先移动y1,再移动y2
            //如果目标位置小于当前位置，先移动y2，再移动y1


            double currentPos ;
            if (!hardware.ArmY[Needle1].getPosition(out currentPos))
                return false;

            if (currentPos < y1Pos)
            {
                hardware.ArmY1.SetPosition(y1Pos);
                hardware.Sleep(200);
                hardware.ArmY2.SetPosition(y2Pos);
            }
            else
            {
                hardware.ArmY2.SetPosition(y2Pos);
                hardware.Sleep(200);
                hardware.ArmY1.SetPosition(y1Pos);
            }

            double[] ypos =  new double[]{y1Pos, y2Pos};
            WaitYPosArrived(ypos);
     
            return true;

        }
        /// <summary>
        /// 移动一根Y轴
        /// </summary>
        /// <param name="yNum">轴</param>
        /// <param name="Pos">位置</param>
        /// <returns></returns>
        public bool MoveArmY(int yNum,   double Pos)
        {
            //
            //如果从左往右则先移动y1,再移动y2，
            //从右往左，先移动y2,再移动y1，

            //移动X轴之前，z轴归零附近，确保Z轴不会与其他部件干涉即可
            System.Diagnostics.Debug.Assert((yNum == 0) || (yNum == 1));

     //       RecordMsg ("开始移动Y轴");
            DailyRecord.writerecord("开始移动Y轴");
            double posZ1;
            double posZ2;
            hardware.ArmZ[Needle1].getPosition(out posZ1);

            hardware.ArmZ[Needle2].getPosition(out posZ2);
     

            if ((posZ1 > (Location.Init_Position_Z + 1))
                || (posZ2 > (Location.Init_Position_Z + 1)))
            {

                double[] zPos = new double[] { Location.Init_Position_Z, Location.Init_Position_Z };
                MoveArmZFixPosition(Location.Init_Position_Z);
                WaitZPosArrived(Needle1, Needle2, zPos);
  
            }
            //如果目标位置 大于当前位置，先移动y1,再移动y2
            //如果目标位置小于当前位置，先移动y2，再移动y1
            double[] currentPos = new double[2] ;
            hardware.ArmY[Needle1].getPosition(out currentPos[Needle1]);

            hardware.ArmY[Needle2].getPosition(out currentPos[Needle2]);
     

            double y1Pos = currentPos[Needle1];
            double y2Pos = currentPos[Needle2];
              
            if (yNum == Needle1)
            {
                y1Pos = Pos;
                if (y1Pos < currentPos[Needle2])
                {
                    y2Pos = y1Pos - 12;
                }
            }
            if (yNum == Needle2)
            {
                y2Pos = Pos;
                if (y2Pos > currentPos[Needle1])
                {
                    y1Pos = y2Pos + 12;
                }
            }

            if (currentPos[yNum] < Pos)
            {
                hardware.ArmY1.SetPosition(y1Pos);
                hardware.Sleep(200);
                hardware.ArmY2.SetPosition(y2Pos);
            }
            else
            {
                hardware.ArmY2.SetPosition(y2Pos);
                hardware.Sleep(200);
                hardware.ArmY1.SetPosition(y1Pos);
            }

            double[] ypos = new double[] { y1Pos, y2Pos };
            WaitYPosArrived(ypos);        
            return true;

        }
    
        /// <summary>
        /// 移动X轴,有重发
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public bool MoveArmX(double pos)
        {
            //移动X轴之前，z轴归零附近，确保Z轴不会与其他部件干涉即可

            double posZ1;
            double posZ2;

            hardware.ArmZ1.getPosition(out posZ1);

            hardware.ArmZ2.getPosition(out posZ2);


            if ((posZ1 > (Location.Init_Position_Z + 1))
                || (posZ2 > (Location.Init_Position_Z + 1)))
            {
                double[] zPos = new double[] { Location.Init_Position_Z, Location.Init_Position_Z };
                MoveArmZFixPosition(Location.Init_Position_Z);
                WaitZPosArrived(Needle1,Needle2,  zPos);
            }

            DailyRecord.writerecord("开始移动X轴");
            hardware.ArmX.SetPosition(pos);
            WaitXPosArrived(pos);

            return true;
        }

        private bool ArmX(double pos)
        {
            //移动X轴之前，z轴归零附近，确保Z轴不会与其他部件干涉即可

            double posZ1;
            double posZ2;
            if (!hardware.ArmZ1.getPosition(out posZ1))
                return false;
            if (!hardware.ArmZ2.getPosition(out posZ2))
                return false;

            if ((posZ1 > (Location.Init_Position_Z + 1))
                || (posZ2 > (Location.Init_Position_Z + 1)))
            {

                MoveArmZFixPosition(Location.Init_Position_Z);
 
            }

            DailyRecord.writerecord("开始移动X轴");
            if (!hardware.ArmX.SetPositionW(pos))
                return false;

            return true;
        }

        /// <summary>
        /// 移动Z轴,并等到位
        /// </summary>
        /// <param name="armZ"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public  bool MoveArmZ(int zNum, double p)
        {
            //             throw new NotImplementedException();

            DailyRecord.writerecord("开始移动Z轴");
            Arm armZ = hardware.ArmZ[zNum];
            armZ.SetPosition(p);

            double []zPos = new double[NeedleMax + 1];
            hardware.ArmZ[Needle1].getPosition(out zPos[Needle1]);
            hardware.ArmZ[Needle2].getPosition(out zPos[Needle2]);

            WaitZPosArrived(zNum, zNum, zPos);
            return true;

        }

 

        /// <summary>
        /// 可选择双针
        /// </summary>
        /// <param name="needle"></param>
        /// <param name="zPos"></param>
        /// <returns></returns>
         public  bool MoveArmZ(bool []needle, double []zPos)
        {
            if (needle[Needle1])
            {
                hardware.ArmZ1.SetPosition(zPos[Needle1]);  //暂时用延时，后期改查询
                Thread.Sleep(10);  //通信时间10ms
            }

            if (needle[Needle2])
            {
                hardware.ArmZ2.SetPosition(zPos[Needle2]);
                Thread.Sleep(10);
                //        hardware.ArmZ3.SetPosition(ZPosition);
            }
            if (needle[Needle1])
            {
                WaitZPosArrived(Needle1, Needle1, zPos);
            }
            if (needle[Needle2])
            {
                WaitZPosArrived(Needle2, Needle2, zPos);
            }

            return true;
        }
        /// <summary>
        /// 所有Z轴移动到固定值
        /// </summary>
        /// <param name="ZPosition"></param>
        /// <returns></returns>
        private void MoveArmZFixPosition(double ZPosition)
        {
            if (GlobalValue.Select_Y1Needle)
            {
                hardware.ArmZ1.SetPosition(ZPosition);  //暂时用延时，后期改查询
                Thread.Sleep(10);  //通信时间10ms
            }

            if (GlobalValue.Select_Y2Needle)
            {
                hardware.ArmZ2.SetPosition(ZPosition);
                Thread.Sleep(10);
                //        hardware.ArmZ3.SetPosition(ZPosition);
            }


            if (GlobalValue.Select_Y1Needle)
            {
                double[] zPos = new double[] { ZPosition, ZPosition };
                WaitZPosArrived(Needle1, Needle1, zPos);

            }
            if (GlobalValue.Select_Y2Needle)
            {
                double[] zPos = new double[] { ZPosition, ZPosition };
                WaitZPosArrived(Needle1, Needle1, zPos);
            }
        }

/// <summary>
/// 0为正在运行，1为到达目标，2异常撞击 ，3检测到液面 4未检测到液面
///5未检测到原点  7左原点极限    8右原点极限  
/// </summary>
/// <param name="arm"></param>
/// <returns></returns>
/*        private bool IsArrived(Arm arm)
        {
            ARM_ERROR_CODE code;
            if (!arm.ArrivedPos(out code))
                return false;
            else
                return true;
        }
 */
        /// <summary>
        /// 玻片加液
        /// </summary>
        /// <param name="slideCtrlList"></param>
        /// <param name="SlideVol"></param>

        protected bool KickOffLiquid(ObservableCollection<SlideControl> slideList, int SlideVol)
        {
            SystemManage.SysManage.setHardwareHost(headtxt);

            //排序
            ObservableCollection<SlideControl> slideCtrlList = new ObservableCollection<SlideControl>(slideList.OrderBy(p => p.Zone * 48 + p.Col * 16 + p.Row));
 //           slideCtrlList.OrderBy(p => p.Area * 24 + p.Col * 12 + p.Row); //排序
            ;
            ObservableCollection<SlideControl>[] SlideColList = new ObservableCollection<SlideControl>[Location.SlidePos_X[zoneNum].Length]; //2列试剂
            for (int i = 0; i < SlideColList.Count(); i++)
            {
                SlideColList[i] = new ObservableCollection<SlideControl>();
            }

            //根据玻片的列编号，将玻片放到不同的列中
            foreach (SlideControl ur in slideCtrlList)
            {
                SlideColList[ur.Col].Add(ur);                              //从第0列开始
            }

            Position pos = new Position();
 
 //           int SlideCounter = 0; //记录多少片玻片

  //          bool Y1FindSlide = false; //Y1是否能找到对应的玻片
  //          bool Y2FindSlide = false;

            bool[] FindSlide = { false, false };

            bool isExportAir = false; //多余的空气是否已排出

            for (int colNo = 0; colNo < SlideColList.Count(); colNo++)   //2列
            {

                //移动X轴
                if (SlideColList[colNo].Count == 0)  //这一列是否有 玻片，如果没有需要的玻片，直接查下一列
                    continue;
                else
                {
  //                  pos.XPos = (Position.X_SlideZoneOffset + colNo * Position.Width_Slide + zoneNum * Position.ZoneInterval);   //仪器的X轴 与 列宽对应
                    pos.XPos = Location.SlidePos_X[zoneNum][colNo];
#if HARDWARE 
                    MoveArmX(pos.XPos);
                    

                    MoveArmZFixPosition(7);
 
#endif
                }        

                //Y轴
                int SlideTotal = SlideColList[colNo].Count;  //该列有多少玻片               

  //              int Y1SlideCount = 0;
  //              int Y2SlideCount = 0;
                int SlideCounter = 0;
                while (SlideCounter < SlideTotal)
                {
                    //移动Y轴
                    //计算Y1、Y2的位置，移动时必须先移动Y1，再移动Y2,
                    //移动Y轴前确认z轴已归零
                    FindSlide[Needle1] = false;
                    FindSlide[Needle2] = false;            

                    //Y2  

                    int RowMaxValue = 11;
                    //第2根针
  //                  Y2SlideCount++;
                    if ((GlobalValue.Select_Y2Needle) && (PumpHaveSlide[Needle2]>0) && (SlideCounter < SlideTotal))//内针无法到最外边
                    {
                         int row = SlideColList[colNo][SlideCounter].Row;
                         RecordMsg("添加" + colNo + "列" + row + "行" + SlideColList[colNo][SlideCounter].SlideInfo.SerialNumber);
      //                  pos.Y2Pos = (Position.Y2_SlideZoneOffset + row * Position.Height_Slide);   //仪器的x轴 与 列宽对应
                        pos.YPos[Needle2] = Location.SlideRowPos_Y[Needle2][row];
                        PumpHaveSlide[Needle2]--;
                        FindSlide[Needle2] = true;
                        SlideCounter++;
                       }

                    //第1根针

 //                   Y1SlideCount++;
                    if ((GlobalValue.Select_Y1Needle) && (PumpHaveSlide[Needle1] > 0) && (SlideCounter < SlideTotal))//内针无法到最外边
                    {
                         int row = SlideColList[colNo][SlideCounter].Row;

                        RecordMsg("添加" + colNo + "列" + row + "行" + SlideColList[colNo][SlideCounter].SlideInfo.SerialNumber);
                   
  //                      pos.Y1Pos = (Position.Y1_SlideZoneOffset + row * Position.Height_Slide);   //仪器的x轴 与 列宽对应
                        pos.YPos[Needle1] = Location.SlideRowPos_Y[Needle1][row];
                       PumpHaveSlide[Needle1] --;
                        FindSlide[Needle1] = true;
                        SlideCounter++;
                     }


                    if (!FindSlide[Needle2])
                    {
                        pos.YPos[Needle2] = pos.YPos[Needle1] - 16;
                    }

                    if (!FindSlide[Needle1])
                    {
                        pos.YPos[Needle1] = pos.YPos[Needle2] + 16;
                    }

#if HARDWARE
                    MoveArmY(pos.YPos, false);

#endif
    

                    //加液
                    if (!isExportAir)  //先排出空气
                    {
#if HARDWARE
                        hardware.PumpFirst.SetVovity();
                        hardware.PumpSecond.SetVovity();
#endif
                        //                      hardware.PumpFirst.Output(airCapacity);
                        RecordMsg("排出空气+试剂");
#if HARDWARE
                        int[] volume = new int[2];

                        volume[Needle1] = smallAirCapacity + SlideVol + 20;
                        volume[Needle2] = smallAirCapacity + SlideVol + 20;
                        SyncOutput(volume, FindSlide, Bottle);
#endif
                        isExportAir = true;
                    }
                    else
                    {
                        // 加试剂
                        RecordMsg("排出试剂");
                        int[] volume = new int[2];
                       
                        if (PumpHaveSlide[Needle2] > 0)
                            volume[Needle2 ] = 0 + SlideVol;  //上次回吸的空气 + 所加试剂
                        else
                            volume[Needle2] = 0 + SlideVol + (int)(bigAirCapacity * 0.7);
                       
                        if (PumpHaveSlide[Needle1] > 0)
                            volume[Needle1] =0 + SlideVol;  //上次回吸的空气 + 所加试剂
                        else
                            volume[Needle1] = 0 + SlideVol + (int)(bigAirCapacity * 0.7);
#if HARDWARE                      ///
                        SyncOutput(volume, FindSlide, Bottle);
#endif

                    }
  //                   hardware.Sleep(700);
            
                    ///回吸，保证不滴液
                     int[] airVolume = new int[2];

                     if(FindSlide[Needle2]) 
                     {
                        airVolume[Needle2 ]  =  0;
                     }
                    else 
                     {
                         airVolume[Needle2 ]  =  0;
                     }
 
                      if(FindSlide[Needle1]) 
                     {
                        airVolume[Needle1]  = 0;
                     }
                      else
                      {
                           airVolume[Needle1]  =  0;
                      }

  //                    if (!SyncSuction(airVolume, FindSlide,Bottle))
  //                      return false ;
                }
            }
 #if HARDWARE
            hardware.PumpFirst.SetDefaultVovity();
            hardware.PumpSecond.SetDefaultVovity();
#endif
            return true ;
        }

        /// <summary>
        /// 将泵内试剂加到固定位置
        /// </summary>
        /// <returns></returns>
        protected bool KickOffAllReagent(Position pos )
        {
  //          SystemManage.SysManage.setHardwareHost(headtxt);

#if HARDWARE 
            MoveArmX(pos.XPos);
            
#endif
                    //移动Y轴
                 bool[] useNeedle = { GlobalValue.Select_Y1Needle, GlobalValue.Select_Y2Needle };

                    //Y2  
                     for (int NeedleNum = Needle1; NeedleNum <= Needle2;NeedleNum ++ )
                     {

                         if (useNeedle[NeedleNum])
                         {

#if HARDWARE
                             double ypos = pos.YPos[NeedleNum];
                             MoveArmY(NeedleNum, ypos);
            
#endif

                             MoveArmZ(NeedleNum, pos.ZPos[NeedleNum]);
       

                             bool[] currentNeedle = { false, false };
                             currentNeedle[NeedleNum] = true;
                             //全部加入
                             SyncResetPump(currentNeedle);


                             MoveArmZ(NeedleNum, Location.Init_Position_Z);
        

  //                           hardware.Sleep(2000);
                         }
                     }

 //                   hardware.Sleep(2000);  //等待2s

                    return true;
        }


        /// <summary>
        /// 两根针分别同时加两种不同的试剂
        /// </summary>
        /// <param name="useSlideList"></param>
        /// <param name="volume"></param>
        /// <returns></returns>
        protected bool KickOffTwoLiquid(ObservableCollection<SlideControl>[] slideList, int SlideVol)
        {
            SystemManage.SysManage.setHardwareHost(headtxt);

            ObservableCollection<SlideControl>[] slideCtrlList = new ObservableCollection<SlideControl>[2];
            ObservableCollection<SlideControl>[][] SlideColList = new ObservableCollection<SlideControl>[2][];  //一根针，一个表

            hardware.PumpFirst.SetVovity();
            hardware.PumpSecond.SetVovity();

            for (int needleIndex = 0; needleIndex < 2; needleIndex++)
            {
                SlideColList[needleIndex] = new ObservableCollection<SlideControl>[Location.SlidePos_X[zoneNum].Length]; //几列试剂，如果是A、B仓就有3列，C、D仓就有2列
            }

            //两个针分别处理
            //
            for (int needleIndex = 0; needleIndex < 2; needleIndex++)
            {
                //排序
                slideCtrlList[needleIndex] = new ObservableCollection<SlideControl>(slideList[needleIndex].OrderBy(p => p.Zone * 24 + p.Col * 12 + p.Row));
                //
                SlideColList[needleIndex] = new ObservableCollection<SlideControl>[Location.SlidePos_X[zoneNum].Length]; //2列试剂

                //每个仓，分成几列
                for (int i = 0; i < SlideColList[needleIndex].Count(); i++)
                {
                    SlideColList[needleIndex][i] = new ObservableCollection<SlideControl>();
                }

                //根据玻片的列编号，将玻片放到不同的列中
                foreach (SlideControl ur in slideCtrlList[needleIndex])
                {
                    SlideColList[needleIndex][ur.Col].Add(ur);                              //从第0列开始
                }
            }

            Position pos = new Position();
//            int SlideCounter = 0; //记录多少片玻片

            //          bool Y1FindSlide = false; //Y1是否能找到对应的玻片
            //          bool Y2FindSlide = false;

            bool[] FindSlide = { false, false };

            bool isExportAir = false; //多余的空气是否已排出

            //遍历每一列，因为1#针和2#针的列数是相同的，所以仅用SlideColList[0]来遍历
            for (int colNo = 0; colNo < SlideColList[0].Count(); colNo++)
            {
 //               SlideCounter = 0;
                //移动X轴
                if ((SlideColList[Needle1][colNo].Count == 0) && (SlideColList[Needle2][colNo].Count == 0))  //这一列是否有 玻片，如果没有需要的玻片，直接查下一列
                    continue;
                else
                {
                    //                  pos.XPos = (Position.X_SlideZoneOffset + colNo * Position.Width_Slide + zoneNum * Position.ZoneInterval);   //仪器的X轴 与 列宽对应
                    pos.XPos = Location.SlidePos_X[zoneNum][colNo];
#if HARDWARE
                    MoveArmX(pos.XPos);

                    MoveArmZFixPosition(7);
#endif
                }

                //Y轴
                int SlideTotal_1Needle = SlideColList[Needle1][colNo].Count;  //该列有多少玻片  
                int SlideTotal_2Needle = SlideColList[Needle2][colNo].Count;  //该列有多少玻片  

                int SlideTotal = Math.Max(SlideTotal_1Needle, SlideTotal_2Needle);

                int Y1SlideCount = 0;
                int Y2SlideCount = 0;

                while (Math.Max(Y1SlideCount,Y2SlideCount) < SlideTotal)
                {
                    //移动Y轴
                    //计算Y1、Y2的位置，移动时必须先移动Y1，再移动Y2,
                    //移动Y轴前确认z轴已归零
                    FindSlide[Needle1] = false;  //该针是否有试剂
                    FindSlide[Needle2] = false;

                    //Y2  
 //                   int RowMaxValue = 11;
                    //第2根针
  //                  Y2SlideCount++;
                    if ((GlobalValue.Select_Y2Needle) && (PumpHaveSlide[Needle2] > 0) && (Y2SlideCount < SlideTotal_2Needle))//内针无法到最外边
                    {
                        int row = SlideColList[Needle2][colNo][Y2SlideCount].Row;
                        RecordMsg("2#针添加" + colNo + "列" + row + "行" + SlideColList[Needle2][colNo][Y2SlideCount].SlideInfo.SerialNumber);
                        //                  pos.Y2Pos = (Position.Y2_SlideZoneOffset + row * Position.Height_Slide);   //仪器的x轴 与 列宽对应
                        pos.YPos[Needle2] = Location.SlideRowPos_Y[Needle2][row];
                        PumpHaveSlide[Needle2]--;
                        FindSlide[Needle2] = true;
                        Y2SlideCount++;
                    }

                    //第1根针

  //                  Y1SlideCount++;
                    if ((GlobalValue.Select_Y1Needle) && (PumpHaveSlide[Needle1] > 0) && (Y1SlideCount < SlideTotal_1Needle))//内针无法到最外边
                    {
                        int row = SlideColList[Needle1][colNo][Y1SlideCount].Row;

                        RecordMsg("1#针添加" + colNo + "列" + row + "行" + SlideColList[Needle1][colNo][Y1SlideCount].SlideInfo.SerialNumber);

                        //                      pos.Y1Pos = (Position.Y1_SlideZoneOffset + row * Position.Height_Slide);   //仪器的x轴 与 列宽对应
                        pos.YPos[Needle1] = Location.SlideRowPos_Y[Needle1][row];
                        PumpHaveSlide[Needle1]--;
                        FindSlide[Needle1] = true;
                        Y1SlideCount++;
                    }

                    //如果Y1<y2;则先加Y1，再加Y2
                    //如果Y1>Y2,则同时加Y1，Y2
                    if (pos.YPos[Needle1] < pos.YPos[Needle2])
                    {
                        if(FindSlide[0])
                        {
                            //先1# 然后2#
                            FindSlide[Needle1] = true;
                            FindSlide[Needle2] = false;
                            pos.YPos[Needle2] = pos.YPos[Needle1] - 16;
    #if HARDWARE
                            MoveArmY(pos.YPos, false);
    #endif
                            isExportAir = ReadyOutputLiquid(SlideVol, FindSlide, isExportAir);
                        }

                        if (FindSlide[1])
                        {
                            //2#
                            FindSlide[Needle1] = false;
                            FindSlide[Needle2] = true;
                            pos.YPos[Needle1] = pos.YPos[Needle2] + 16;
#if HARDWARE
                            MoveArmY(pos.YPos, false);
#endif
                            isExportAir = ReadyOutputLiquid(SlideVol, FindSlide, isExportAir);
                        }
                    }
                    else
                    {
#if HARDWARE
                        MoveArmY(pos.YPos, false);
#endif
                        isExportAir = ReadyOutputLiquid(SlideVol, FindSlide, isExportAir);
                    }

                    //                   hardware.Sleep(700);
                }
            }

            hardware.PumpFirst.SetDefaultVovity();
            hardware.PumpSecond.SetDefaultVovity();
            return true;
        }

        /// <summary>
        /// 开始去加液
        /// </summary>
        /// <param name="SlideVol"></param>
        /// <param name="FindSlide"></param>
        /// <param name="isExportAir"></param>
        /// <returns></returns>
        private bool ReadyOutputLiquid(int SlideVol, bool[] FindSlide, bool isExportAir)
        {
            //加液
            if (!isExportAir)  //先排出空气
            {
                //                      hardware.PumpFirst.Output(airCapacity);
                RecordMsg("排出空气+试剂");
                int[] volume = new int[2];
                volume[Needle1] = smallAirCapacity + SlideVol + 20;
                volume[Needle2] = smallAirCapacity + SlideVol + 20;
#if HARDWARE
                SyncOutput(volume, FindSlide, Bottle);
#endif
                isExportAir = true;
            }
            else
            {
                // 加试剂
                RecordMsg("排出试剂");
                int[] volume = new int[2];

                if (PumpHaveSlide[Needle2] > 0)
                    volume[Needle2] = 0 + SlideVol;  //上次回吸的空气 + 所加试剂
                else
                    volume[Needle2] = 0 + SlideVol + (int)(bigAirCapacity * 0.7);  //如果是最后一片，多吐一些液体

                if (PumpHaveSlide[Needle1] > 0)
                    volume[Needle1] = 0 + SlideVol;  //上次回吸的空气 + 所加试剂
                else
                    volume[Needle1] = 0 + SlideVol + (int)(bigAirCapacity * 0.7);
                ///
                #if HARDWARE
                   SyncOutput(volume, FindSlide, Bottle);
                #endif
            }


            ///回吸，保证不滴液
            /*改部分代码已删除*/

            return isExportAir;
        }


        /// <summary>
        /// 吸液
        /// </summary>
        /// <param name="volume"></param>
        /// <param name="FindSlide"></param>
        /// <returns></returns>
        protected bool SyncOutput(int[] volume, bool[] FindSlide,int Dir)
        {
            int para;
            SYRINGE_STATUS status;
            //控制阀
            if (FindSlide[Needle2])
            {
                if (Dir == Bottle)
                {
                    hardware.syringePump[Needle2].OpenValveLeft();
                       
                }
                else
                {
                    hardware.syringePump[Needle2].OpenValveRight();
                       
                }
            }

            if (FindSlide[Needle1])
            {
                if (Dir == Bottle)
                {
                    hardware.syringePump[Needle1].OpenValveLeft();
   
                }
                else
                {
                    hardware.syringePump[Needle1].OpenValveRight();
                      
                }
            }
            //等待阀到位
            if (FindSlide[Needle2])
            {
                status = hardware.syringePump[Needle2].WaitExeResult(SyringePump.VALVEUnit, out para);
                if (SYRINGE_STATUS.SUCCESS != status)
                {
                    throw new HardwareException("注射泵2工作过程中，阀故障。" + "故障代码:" + status, ErrorHardware.Syringe[Needle2], "故障原因：" + status);

//                    DisPlayErrorMsg("注射泵2吐液时，阀切换故障。" + "故障代码:" + status);
//                    return false;
                }
            }
            if (FindSlide[Needle1])
            {
                status =  hardware.syringePump[Needle1].WaitExeResult(SyringePump.VALVEUnit, out para);
                if (SYRINGE_STATUS.SUCCESS != status)
                {
                    throw new HardwareException("注射泵1工作过程中，阀故障。" + "故障代码:" + status, ErrorHardware.Syringe[Needle1], "故障原因：" + status);

 //                   DisPlayErrorMsg("注射泵1吐液时，阀切换故障。" + "故障代码:" + status);
 //                   return false;
                }
            }

            //控制泵
            if (FindSlide[Needle2])
            {
#if HARDWARE
                if (!hardware.syringePump[Needle2].Output(volume[Needle2]))
                {
                    return false;
                }
#endif
            }
            if (FindSlide[Needle1])
            {
#if HARDWARE
                if (!hardware.syringePump[Needle1].Output(volume[Needle1]))
                {
                    return false;
                }
#endif
            }


            //等待泵到位
            if (FindSlide[Needle2])
            {
                status = hardware.syringePump[Needle2].WaitExeResult(SyringePump.PUMPUnit, out para);
                    if (SYRINGE_STATUS.SUCCESS != status)
                    {
                        throw new HardwareException("注射泵2工作过程中，泵故障。" + "故障代码:" + status, ErrorHardware.Pump[Needle2], "故障原因：" + status);

 //                       DisPlayErrorMsg("注射泵2吐液时，泵故障。" + "故障代码:" + status);
 //                       return false;
                    }
            }
            if (FindSlide[Needle1])
            {
                status = hardware.syringePump[Needle1].WaitExeResult(SyringePump.PUMPUnit, out para);
                if (SYRINGE_STATUS.SUCCESS != status)
                {
                    throw new HardwareException("注射泵1工作过程中，泵故障。" + "故障代码:" + status, ErrorHardware.Pump[Needle1], "故障原因：" + status);

//                    DisPlayErrorMsg("注射泵1吐液时，泵故障。" + "故障代码:" + status);
//                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 复位
        /// </summary>
        /// <param name="volume"></param>
        /// <param name="FindSlide"></param>
        /// <returns></returns>
        protected bool SyncResetPump(bool[] FindSlide)
        {
            int para;
            SYRINGE_STATUS status;
            //控制阀
            if (FindSlide[Needle2])
            {
                hardware.syringePump[Needle2].OpenValveLeft();
                   
            }

            if (FindSlide[Needle1])
            {
                hardware.syringePump[Needle1].OpenValveLeft();
                                 
            }
            //等待阀到位
            if (FindSlide[Needle2])
            {
                status = hardware.syringePump[Needle2].WaitExeResult(SyringePump.VALVEUnit, out para);
                if (SYRINGE_STATUS.SUCCESS != status)
                {
  //                  DisPlayErrorMsg("注射泵2复位过程中，阀故障。" + "故障代码:" + status);
                    throw new HardwareException("注射泵2复位过程中，阀故障。" + "故障代码:" + status, ErrorHardware.Syringe[Needle2], "故障原因：" + status);
                    //                   return false;
                }
            }
            if (FindSlide[Needle1])
            {
                status = hardware.syringePump[Needle1].WaitExeResult(SyringePump.VALVEUnit, out para);
                if (SYRINGE_STATUS.SUCCESS != status)
                {
                    throw new HardwareException("注射泵1复位过程中，阀故障。" + "故障代码:" + status, ErrorHardware.Syringe[Needle1], "故障原因：" + status);
 
   //                 DisPlayErrorMsg("注射泵1复位过程中，阀故障。" + "故障代码:" + status);
   //                 return false;
                }
            }

            //控制泵复位
            if (FindSlide[Needle2])
            {
#if HARDWARE
                hardware.syringePump[Needle2].pumpGohome();
                    
#endif
            }
            if (FindSlide[Needle1])
            {
#if HARDWARE
                hardware.syringePump[Needle1].pumpGohome();
 
#endif
            }


            //等待泵到位
            if (FindSlide[Needle2])
            {
                status = hardware.syringePump[Needle2].WaitExeResult(SyringePump.PUMPUnit, out para);
                if (SYRINGE_STATUS.SUCCESS != status)
                {
                    throw new HardwareException("注射泵2复位过程中，泵故障。" + "故障代码:" + status, ErrorHardware.Pump[Needle2], "故障原因：" + status);
 
  //                  DisPlayErrorMsg("注射泵2复位时，泵故障。" + "故障代码:" + status);
  //                  return false;
                }
            }
            if (FindSlide[Needle1])
            {
                status = hardware.syringePump[Needle1].WaitExeResult(SyringePump.PUMPUnit, out para);
                if (SYRINGE_STATUS.SUCCESS != status)
                {
                    throw new HardwareException("注射泵1复位过程中，泵故障。" + "故障代码:" + status, ErrorHardware.Pump[Needle1], "故障原因：" + status);
 
 //                   DisPlayErrorMsg("注射泵1复位时，泵故障。" + "故障代码:" + status);
 //                   return false;
                }
            }


            return true;
        }

        //切换线程时洗针
        protected bool SwtichThreadWashNeedle()
        {
            //如果未洗针，而且线程变化了，则洗针
            if (!hardware.currentThreadIDEqualLatID())
            {
#if HARDWARE
                RecordMsg("*****************切换线程");
                if (!RestStation())
                    return false;

                if (step.Reagent != GlobalValue.AlreadyUsedReagent)
                {
                    StartWashNeedle(hardware.NeedleIsWashed); //洗针                          
                }
#endif
            }

            return true;
        }
/// <summary>
/// 开始洗针
/// </summary>
/// <param name="ShallowDeep">1为浅，2为深，0为已洗针</param>
        public void StartWashNeedle(int ShallowDeep, double Washtime = 3.0)
        {

            if (hardware.NeedleIsWashed == Washed)
            {
                return;
            }

            RecordMsg("开始洗针");
            SystemManage.SysManage.setHardwareHost(headtxt);
           
            //洗站在固定位置

            Position pos = new Position();
    
            RecordMsg("洗针内1s");
#if HARDWARE
            WaterWash(500);
 
#endif
            if(ShallowDeep == Shallow)
               pos.XPos = Location.WashStation_Showder_X;
            else
                pos.XPos = Location.WashStation_Deep_X;

#if HARDWARE
            MoveArmX(pos.XPos); 
#endif
            //移动y轴  //固定位置
            if(ShallowDeep == Shallow)
            {
                pos.YPos[Needle1] = Location.WashStationShallow_Y[Needle1];
                pos.YPos[Needle2] = Location.WashStationShallow_Y[Needle2];
            }
            else if (ShallowDeep == Deeper)
            {
                pos.YPos[Needle1] = Location.WashStationDeeper_Y[Needle1];
                pos.YPos[Needle2] = Location.WashStationDeeper_Y[Needle2];
            }
            else
            {
                //do nothing
                throw new Exception("未知洗站类型"); 
            }
  
#if HARDWARE
            MoveArmY(pos.YPos);
#endif
            //Z轴下探到底部
            double MaxHepthSlide = Location.MaxDepthWsShallow; ;
            if (ShallowDeep == Shallow)
            {

                    MaxHepthSlide = Location.MaxDepthWsShallow - 5.0; //Z轴加样时的下降的最大长度

             }
            else if (ShallowDeep == Deeper)
            {
                MaxHepthSlide = Location.MaxDepthWsDeeper - 10.0; //Z轴加样时的下降的最大长度
            }

            msg = "移动Z轴到" + MaxHepthSlide + "mm";
            RecordMsg(msg);

#if HARDWARE    
            MoveArmZFixPosition(MaxHepthSlide);

#endif

            RecordMsg("洗外针");
#if HARDWARE  

            WaterWash((int)(Washtime * 1000));
  
#endif        
            //拔出针
            RecordMsg("移动Z轴，拔出针");
#if HARDWARE      
            MoveArmZFixPosition(Location.Init_Position_Z);
     
#endif


 //          hardware.Sleep(1 * 1000);

            PumpHaveSlide[0] = 0;
            PumpHaveSlide[1] = 0;

            RecordMsg("洗针结束开始等待");

        }

        /// <summary>
        /// 吸水洗针
        /// </summary>
        protected void WaterWash(int volume)
        {
            int para;
            //先吸液再吐液
            bool[] usedNeedle = {GlobalValue.Select_Y1Needle ,GlobalValue.Select_Y2Needle };
            int[] waterVolume = { volume, volume };
            SyncSuction(waterVolume, usedNeedle, Bucket);


            //
   //         hardware.Sleep(1000);

            SyncOutput(waterVolume, usedNeedle, Bottle);
  
    //        hardware.Sleep(1000);

        }

        /// <summary>
        /// 计算实际的等待时间
        /// </summary>
        /// <param name="stepTime">步骤的等待时间</param>
        /// <param name="kickLiquidTime">加液时间</param>
        /// <returns></returns>
       protected int getActualWaitingTime(int stepTime, int kickLiquidTime)
        {
                 int actureTime;
 /*               if (hardware.NeedleIsWashed == Washed)
                    actureTime = stepTime - WashNeedleTime - kickLiquidTime;
                else
                    actureTime = stepTime - kickLiquidTime;  
*/
                 actureTime = stepTime - kickLiquidTime;  
                if (actureTime < 0)
                    actureTime = 0;

                return actureTime;
        }

        /// <summary>
        /// 等待时间，并释放硬件控制权，单位秒
        /// </summary>
        /// <param name="WaitTime"></param>
        /// 
        protected void Waiting(int setTime)
        {
            // throw new NotImplementedException();
            int WaitTime = setTime;
             hardware.RealeseHardwareHandle();

             string recordMsg;
             int waitLongtime = OperateTimeoutSetting.Timeout - 10;
 /* 
             Thread warningthread = new Thread(new ThreadStart(startMethod));
             warningthread.Start();
*/

             WorkStatus status = WorkStatus.getInstance();

            //等待时间
             GlobalValue.CountWaitingforTime++;

             recordMsg = String.Format("{0}#仓总等待时间：{1} ",(zoneNum + 1), WaitTime);
             DailyRecord.writerecord(recordMsg); 
             while ((WaitTime > 0) || (status.Status[zoneNum] == WorkStatus.Pause))
             {
                 recordMsg = String.Format("{0}#仓剩余等待时间：{1} ", (zoneNum + 1), WaitTime);
                 DailyRecord.writerecord(recordMsg); 
                 hardware.Sleep(1000);
                 if (WaitTime > 0)
                 {
                     WaitTime--;
                 }

                 if (status.Status[zoneNum] == WorkStatus.Pause)
                 {
                     recordMsg = String.Format("{0}#仓正处于暂停状态，剩余等待时间：{1} ", (zoneNum + 1), WaitTime);
                 }
             }
            GlobalValue.CountWaitingforTime--;


             OperateProcess currentPro = new OperateProcess(zoneNum,Priority,OrderID);
             GlobalValue.ProcessList.Add(currentPro);   //将该线程放入等待队列
             recordMsg = String.Format("{0}#仓 进入就绪队列，优先级Priority为 ：{1}  OrderID: {2}，等待获取机械臂等操作权限.....", (zoneNum + 1), Priority, OrderID);
             DailyRecord.writerecord(recordMsg); 

  //           hardware.Sleep(10);
      
             while (true)
             {
                 hardware.Sleep(100);
                 if (!hardware.UsingAllDevice)  //机械臂是否空闲
                 {
                        hardware.GetHardwareHandle(); //获取权     
 #if !HARDWARE                
                         hardware.Sleep(100000);
#endif
                         recordMsg = String.Format("{0}#仓是否为最大优先级: ", (zoneNum + 1));
                         DailyRecord.writerecord(recordMsg);
                         if (getMaxProcess() == currentPro)
                         {
                             break;
                         }
                         else
                         {
                             recordMsg = String.Format("{0}#仓不是最大优先级: ", (zoneNum + 1));
                             DailyRecord.writerecord(recordMsg);
                             hardware.RealeseHardwareHandle();
                         }                   
                 }

                 //
                 if ((setTime>0) && (DisplayTimeout) && (OperateTimeoutSetting.RemindGloableEn))
                 {
                     waitLongtime++;
                     if (waitLongtime > OperateTimeoutSetting.Timeout * 10)  //提示间隔时间  (*10 表示多少个100ms)
                     {
                         waitLongtime = 0;
                         Thread warningthread = new Thread(new ThreadStart(startMethod));
                         warningthread.Start();
                     }
                 }

             } 
             GlobalValue.ProcessList.Remove(currentPro);
             recordMsg = String.Format("{0}#仓 已获取机械臂等操作权限，退出就绪队列，其优先级Priority为 ：{1}  OrderID: {2}", (zoneNum + 1), Priority, OrderID);
             DailyRecord.writerecord(recordMsg);
        }

        private void startMethod()
        {
            Console.Beep(1000,500);
            string recordMsg = (zoneNum + 1) + "仓 " + step.StepName + "步骤" + " 时间已到";
            DailyRecord.writerecord(recordMsg);
            MessageBox.Show(recordMsg);
            
        }

        OperateProcess getMaxProcess()
        {

            OperateProcess maxPro;
            maxPro = GlobalValue.ProcessList[0];
            DailyRecord.writerecord("正在排队的线程数量:" + GlobalValue.ProcessList.Count);
         
            foreach (OperateProcess pro in GlobalValue.ProcessList)
            {
                if (pro.Priority > maxPro.Priority)
                {
                    maxPro = pro;
                }
                else if (pro.Priority == maxPro.Priority)
                {
                    if (pro.OrderID < maxPro.OrderID)
                    {
                        maxPro = pro;
                    }
                }
                DailyRecord.writerecord((pro.Num+1) + "#仓，优先级" + "Priority:" + pro.Priority + "     OrderID:" + pro.OrderID); 
            }
            DailyRecord.writerecord("优最大先级" + "Priority:" + maxPro.Priority + "   OrderID:" + maxPro.OrderID + "    所在的仓:" + (maxPro.Num + 1)); 
            return maxPro; 
        }

        //针休息时的位置，在洗站上方
        protected bool RestStation()
        {
#if HARDWARE

            Position pos = new Position();
            pos.XPos = Location.Idle_X;
            MoveArmX(pos.XPos); 
            
            Thread.Sleep(100);
            pos.YPos[Needle1] = Location.WashStationShallow_Y[Needle1];
            pos.YPos[Needle2] = Location.WashStationShallow_Y[Needle2];
#endif

#if HARDWARE
            MoveArmY(pos.YPos); 



            //           hardware.PumpSecond.Reset();
            //          hardware.PumpFirst.Reset();

             PumpHaveSlide[0] = 0;
             PumpHaveSlide[1] = 0;
#endif

             //泵复位
             RecordMsg("泵复位");
#if HARDWARE
  //           MoveArmZFixPosition(40);
             bool[] usedNeedle = { GlobalValue.Select_Y1Needle, GlobalValue.Select_Y2Needle };
             if (!SyncResetPump(usedNeedle))
                 return false;
#endif
             return true;
        }
        /// <summary>
        /// 开始加热,加热到指定温度
        /// </summary>
        /// <param name="incubatorNum"></param>
        /// <param name="tempreture"></param>
        /// <param name="time"></param>
        /// <returns></returns>

        protected void StartHeat(int incubatorNum, double tempreture, int keepTime)
        {
  
            Incubator incubator = hardware.IncubatorGrop[incubatorNum];
            //设置温度
            double temp = tempreture;
            msg = "设置温度：" + temp.ToString() + "℃";
            RecordMsg(msg);


            msg = "设置时间：" + keepTime.ToString() + "秒";
            RecordMsg(msg);

  
            //获取温度
            double CurrentTemp;
            CurrentTemp = incubator.getTemprature();

            int heatTimeMax = 30;
            DateTime startTime = DateTime.Now;

            double upperBias = 2;  //上偏差
            double lowBias = 2;    //下偏差

            if (temp < 81)
            {
                upperBias = 2;
                lowBias = 2;
            }

            while ((CurrentTemp < (temp - lowBias)) || (CurrentTemp > (temp + upperBias)))  //长时间达不到指定温度，则应报错
            {
                // 丢帧无影响，不报错
                if (CurrentTemp < temp)
                {
                     incubator.setTemprature(temp);

                     incubator.setTime(keepTime);  //维持温度 的时间

                    hardware.Sleep(10);
                    incubator.Heat();

                    DateTime endTime = DateTime.Now;
                    TimeSpan ts = endTime - startTime;
   //                 //如果是脱蜡，大于3min，并且温度再5°范围内，就继续执行。
                    if ((ts.Minutes > 3) && (step.StepName == OperateType.Dew) &&(CurrentTemp > (temp - 5)))
                    {
                        break;
                    }

                    if (ts.Minutes > heatTimeMax)
                    {
                         RecordMsg("加热器故障");
                        ErrorMsgResult rs = DisPlayErrorMsg("已加热" +heatTimeMax + "分钟，未达到指定温度，是否继续执行？\n\t（重试）继续等待 \n\t(继续)继续往下执行 \n\t （否）中断该规程 ", "加热错误", MessageInfoButton.RETRYIGNORENO);

                        if (rs == ErrorMsgResult.Continue)
                        {
                            heatTimeMax = heatTimeMax + 5; //加5min
                            break;
                        }
                        else if (rs == ErrorMsgResult.Abort)
                        {
                          //  incubator.stopHeat();
                            StopHeat(incubatorNum);
                            throw new HardwareException( (zoneNum+1) + "孵育仓加热超时",ErrorHardware.Mainboard[zoneNum], "主控制板或继电器故障");
                        }

                    }

                }

               hardware.Sleep(2000);
    //            Waiting(10);  //可插入其他线程

               CurrentTemp = incubator.getTemprature();

                if (CurrentTemp > 110)
                {
                    StopHeat(incubatorNum);              
                   msg = (zoneNum+1) + "号孵育仓" + "过热，当前温度为：" + CurrentTemp.ToString() + "℃";
                   RecordMsg(msg);
                   throw new HardwareException( msg, ErrorHardware.Mainboard[zoneNum], "主控制板或继电器故障");
                
                }
            }     
        }

        protected bool StopHeat(int incubatorNum)
        {
            Incubator incubator = hardware.IncubatorGrop[incubatorNum];
            incubator.setTemprature(20);
            incubator.setTime(0);
            incubator.stopHeat();
            return true;
        }

        public static int HeatingCount = 0;
        /// <summary>
        /// 打开风扇
        /// </summary>
        /// <returns></returns>
        protected bool OpenFan()
        {
            HeatingCount++;
            Incubator incubator = hardware.IncubatorGrop[0];
            if(incubator.FanON())
              return true;
            else
            {
                CloseFan();
                return false;
            }
        }

        /// <summary>
        /// 关闭风扇
        /// </summary>
        /// <returns></returns>
        protected bool CloseFan()
        {
            HeatingCount--;
            if (HeatingCount > 0)
                return true;

            Incubator incubator = hardware.IncubatorGrop[0];
            if (incubator.FanOFF())
                return true;
            else
            {
                return false;
            }
        }

        protected ObservableCollection<UsingReagentInformation> FindReagent(String strReagent, bool includeEmptyBottle = false)
        {
            String reagentName = "";
            String reagentColone = "";
            String reagentCategory = "";
            //获取试剂类型：试剂名字 _ 克隆号_ 规格


            //试剂是否存在
            bool reagentFind = false;
 //           ReagentInformation ReadyFindReagent = DataCollection.GetReagent("ReagentID", strReagent);//获取试剂的完整信息
            //          Position reagentPos = null;
            ObservableCollection<UsingReagentInformation> usingReagentList = new ObservableCollection<UsingReagentInformation>();
            usingReagentList = DataCollection.GetAllUsingReagents();

            ObservableCollection<UsingReagentInformation> findReagentList = new ObservableCollection<UsingReagentInformation>();
  //       List<UsingReagentInformation> findReagentList = new List<UsingReagentInformation>();

            foreach (UsingReagentInformation uingReagent in usingReagentList)  //遍历所有上架试剂
            {
                ReagentInformation reagent = DataCollection.GetReagent("ReagentID", uingReagent.ReagentID);//获取试剂的完整信息

                if (reagent != null)
                {

/*                    reagentFind = ((ReadyFindReagent.ReagentName == reagent.ReagentName) &&
                                         (ReadyFindReagent.ContentsNum == reagent.ContentsNum) &&
                                         (ReadyFindReagent.ShortName == reagent.ShortName) &&
                                         !reagent.IsEmpty);
  */
                    if (!includeEmptyBottle)
                        reagentFind = ((strReagent == reagent.SpecificationMark) && (!reagent.IsEmpty));
                    else
                        reagentFind = (strReagent == reagent.SpecificationMark);

                    if (reagentFind)
                    {
                        //判断试剂的容量是否满足要求

                        //根据位置计算试剂在设备中的位置，单位：mm
                        findReagentList.Add(uingReagent);

                        //                      break;

                    }
                }

            }
   //         findReagentList.OrderBy(p=>p.Row); //排序
   //          findReagentList.OrderByDescending(p => p.Row); //排序

            ObservableCollection<UsingReagentInformation> findList = new ObservableCollection<UsingReagentInformation>(findReagentList.OrderBy(p => p.Row));
             return findList;
        }

        /// <summary>
        /// 加水
        /// </summary>
        /// <param name="waterVolumeMax">最大加水量（单位ml）</param>
        /// <param name="waterState">水的状态，0为纯净水，1为冰水</param>
        /// <returns></returns>
        protected bool AddWater(int waterVolumeMax, int waterState = CoolWater)
        {

            hardware.GetWaterPipeHandle();

            //小锅的加水量按比例减小
 //           DataCollection.ReadTimeSetting(GlobalValue.ConfigFileName);
            double BigToSmallPercent = 1;
            if (zoneNum > 1)
            {
                BigToSmallPercent = (double)DevicePara.PourWaterShort / (double)DevicePara.PourWaterLong;
            }

            waterVolumeMax = (int)((double)waterVolumeMax * BigToSmallPercent);

            RecordMsg("开始加液....");
            Incubator incubator = (hardware.IncubatorGrop)[zoneNum];
#if HARDWARE
            incubator.stopWater();
            hardware.Sleep(100);
            if (0 == waterState)
            {
                RecordMsg("正在加水....");
                incubator.addWater(); 
            }
            else                                //冰水
            {
                RecordMsg("正在加修复液....");
                if (!incubator.openEDTA())  //
                {
                    CloseFan();

                }
            }
#endif
            int waterVolume = 0;
            {
                RecordMsg("加水量：" + waterVolumeMax);
                int waterTimeMax = waterVolumeMax / 20;  //需要多少时间

                DateTime startTime = DateTime.Now;
                TimeSpan ts = DateTime.Now - startTime;
                while (ts.Seconds < waterTimeMax)  //此处不应该一直等，万一传感器坏了
                {
 //                   hardware.Sleep(1000);  //如果加水过程中暂停，有问题
                    Thread.Sleep(1000);
                    ts = DateTime.Now - startTime;
                    waterVolume = ts.Seconds * 20;  //1s钟10ml
                    RecordMsg("已加水：" + waterVolume);

                     WorkStatus status = WorkStatus.getInstance();
                     if (status.Status[zoneNum] == WorkStatus.Stopping)
                     {
                         break;
                     }
                }
            }

            //停止
            incubator.stopWater();
            RecordMsg("停止加水");

            hardware.ReleaseWaterPipeControl();

            return true;
        }

        public void tansReagentEmptyEvent(ReagentInformation rg)
        {
            ReagentEmpty(rg);
        }

        public void tansReagentPauseEvent(ReagentInformation rg)
        {
            CreatePauseEvent();
        }

       override public  string ToString()
        {
            return ("*******************第" + (zoneNum+1) + "孵育仓**" + "  第" + step.StepNum + "步（" + step.StepName + "） ********************");
        }
        //错误信息
        /*
        protected String GetErrorMsg(ERROR_STATUS error)
        {
            string errorMsg;
            switch(error)
            {
                case ERROR_STATUS.ARM_X_MOVE_ERROR:
                    {
                        errorMsg = "X轴故障！程序终止。";
                    }
                    break;
                case ERROR_STATUS.ARM_Y1_MOVE_ERROR:
                    {
                        errorMsg = "Y1轴故障！程序终止。";
                    }
                    break;
                case ERROR_STATUS.ARM_Y2_MOVE_ERROR:
                    {
                        errorMsg = "Y2轴故障！程序终止。";
                    }
                    break;
                case ERROR_STATUS.ARM_Z1_DETECT_ERROR:
                    {
                        errorMsg = "Z1轴探液失败！程序终止。";
                    }
                    break;
                case ERROR_STATUS.ARM_Z1_MOVE_ERROR:
                    {
                        errorMsg = "Z1轴故障！程序终止。";
                    }
                    break;
                case ERROR_STATUS.ARM_Z2_DETECT_ERROR:
                    {
                        errorMsg = "Z2轴探液失败！程序终止。";
                    }
                    break;
                case ERROR_STATUS.ARM_Z2_MOVE_ERROR:
                    {
                        errorMsg = "Z2轴故障！程序终止。";
                    }
                    break;
                case ERROR_STATUS.NEEDLE_NOEXIST:
                    {
                        errorMsg = "使用了不存在的取液针！";
                    }
                    break;
                case ERROR_STATUS.NOFIND_REAGEAT:
                    {
                        errorMsg = "未发现某种试剂";
                    }
                    break;
                case ERROR_STATUS.SYRINGE_NOEXIST:
                    {
                        errorMsg = "使用了不存在的注射泵";
                    }
                    break;
                case ERROR_STATUS.SYRINGE_P1_COM_ERROR:
                    {
                        errorMsg = "1#注射泵通信故障";
                    }
                    break;
                case ERROR_STATUS.SYRINGE_P1_PUMP_ERROR:
                    {
                        errorMsg = "1#注射泵故障";
                    }
                    break;
                case ERROR_STATUS.SYRINGE_P1_WIRE_ERROR:
                    {
                        errorMsg = "1#注射泵线路故障";
                    }
                    break;
                case ERROR_STATUS.SYRINGE_P2_COM_ERROR:
                    {
                        errorMsg = "2#注射泵通信故障";
                    }
                    break;
                case ERROR_STATUS.SYRINGE_P2_PUMP_ERROR:
                    {
                        errorMsg = "2#注射泵故障";
                    }
                    break;
                case ERROR_STATUS.SYRINGE_P2_WIRE_ERROR:
                    {
                        errorMsg = "2#注射泵线路故障";
                    }
                    break;
                default:
                    errorMsg = "未知的错误类型。";
                    break;

            }

            return errorMsg;
        }
        */

    }


}
