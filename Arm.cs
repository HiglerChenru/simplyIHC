using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Windows;
using System.Windows.Threading;
using System.Threading;
using Higler.Classes;

namespace Higler
{
    public enum ARM_CMD_CODE
    {
        RESET = 'G',
        RESET_QUERY = 'g',
        POSITION = 'D',
        POSITION_QUERY= 'd',
        POSITION_GET = 'E',
        LIQUIDLEVEL_DETECT='j',
        STOP = 'K',
        ReleaseMotor = 'a'
    }

    /// <summary>
    /// /// 0为正在运行，1为到达目标，2异常撞击 ，3检测到液面 4未检测到液面
    ///5未检测到原点  7左原点极限    8右原点极限  
    /// </summary>
    public enum ARM_ERROR_CODE
    {
        RUNNING = 0,
        ARRIVED = 1,
        CRASH = 2,
        DETECTED = 3,
        NO_DETECTED = 4,
        NO_ORIGIN = 5,
        LEFT_LIMIT = 7,
        RIGHT_LIMIT = 8,
        COM_ERROR = 9,
        SEDN_FAIL = 10,
        NO_ANSWER = 11,

        UNKNOW_ERROR = 20
    }

    public class Arm
    {
        SerialCom com;
        string armName;

        public string ArmName
        {
            get { return armName; }
            set { armName = value; }
        }
        byte armAddr;

        public byte ArmAddr
        {
            get { return armAddr; }
        }
        bool haveProbe;

        bool isMsgEcho = false;
        byte[] receivedData = null;  //接收的数据
 //       private UInt32 position;
        private int maxDistance_pls = 23000;

        public int MaxDistance_pls
        {
            get { return maxDistance_pls; }
            set { maxDistance_pls = value;
            maxDistance_mm = maxDistance_pls / scale;
            }
        }

        private double maxDistance_mm;

        public double MaxDistance_mm
        {
            get { return maxDistance_mm; }
            set { maxDistance_mm = value;
            maxDistance_pls = (int)(maxDistance_mm * scale);
            }
        }
    

        //委托 事件
    public delegate void DisplayReceive(string strData);
        public event DisplayReceive armReceive;

        public delegate void DisplaySend(string strData);
        public event DisplaySend armSend;

        //定时器
        private DispatcherTimer ticker = null;   //定时器
        private string strReadySendMsg = null;  //待发送的信息
        private const byte FRAME_HEAD_Arm484 = (byte)(0x3E);
        private double scale; //比例系数

        public double Scale
        {
          get { return scale; }
        }



        public Arm(SerialCom com, string strName, double maxRange,double scale, bool haveProbe=false)
        {
            this.armName = strName;
            this.com = com;
            this.scale = scale;  //pls与mm的比例
            this.MaxDistance_mm = maxRange;
            this.haveProbe = haveProbe;
 
            this.armAddr = getArmAddr(strName);
          }
 
    
       private byte getArmAddr(string strName)
      {
           byte addr = 0;

           switch(strName)
           {
               case "ArmZ1":
                   addr = 01;
                   break;
                case "ArmZ2":   //硬件地址与3相反
                   addr = 02;
                   break;
   
                case "ArmY1":
                   addr = 03;
                   break;
                case "ArmY2":
                   addr = 04;
                   break;
                case "ArmX":
                   addr = 05;
                   break;
                default:
                   MessageBox.Show("无效的电机编号");
                   break;
           }
           return addr;
       }
        /// <summary>
        /// 机械臂轴复位
        /// </summary>
        /// <returns></returns>
        public void Reset()
       {
            const char chOprateCode = (char)ARM_CMD_CODE.RESET;
             char chMotorAddr = (char)getArmAddr(armName);//  电机地址

             SendMessage(chMotorAddr, chOprateCode);

       }
        /// <summary>
        /// 释放励磁
        /// </summary>
        /// <returns></returns>
        public void ReleaseMotor()
        {
            const char chOprateCode = (char)ARM_CMD_CODE.ReleaseMotor;
            char chMotorAddr = (char)getArmAddr(armName);//  电机地址

            SendMessage(chMotorAddr, chOprateCode);

        }
        /// <summary>
        /// 查询是否回零成功，
        /// 0——正在回零 1 回零完成 2 回零失败 3通信错误
        /// </summary>
        /// <returns></returns>
        public ARM_ERROR_CODE readInf(ARM_CMD_CODE code)
        {
           char chOprateCode = (char)code;
           char chMotorAddr = (char)getArmAddr(armName);//  电机地址

           isMsgEcho =false;
           int MsgCounter = 0;

              try{
                      SendMessage(chMotorAddr, chOprateCode);
                }
                catch(HardwareException ex)
                {
                     SendMessage(chMotorAddr, chOprateCode);
                }
              

            string strReturn = null;
            if (receivedData != null)  //未算校验
            {
                strReturn += (char)receivedData[4];   //获取应答状态
                strReturn += (char)receivedData[5];

                return (ARM_ERROR_CODE)FormatValue.string2Int(strReturn);  //
            }
            else
            {
  //              DisPlayMsg("无应答");
                return ARM_ERROR_CODE.NO_ANSWER;
            }
         }

        /// <summary>
        /// 查询位置，
        /// 
        /// </summary>
        /// <returns></returns>
        public bool getPosition(out double pos)
        {
            pos = 0;
            char chOprateCode = (char)ARM_CMD_CODE.POSITION_GET;
            char chMotorAddr = (char)getArmAddr(armName);//  电机地址
            int pos_pls = 0;
            
            //
           while(true)
           {
              SendMessage(chMotorAddr, chOprateCode);

              if ((char)receivedData[3] == (char)ARM_CMD_CODE.POSITION_GET)
                  break;
              else
              {
                  ////如果返回的命令字不符，则记录接收的帧，并重发。
                  String rcvData = "";
                  foreach (byte abyte in receivedData)
                      //                 rcvData = rcvData +"_" + FormatValue.Hex16ToString(abyte);
                      rcvData = rcvData + (char)abyte;

                  ErrorRecord.writerecord(rcvData);
                  ErrorRecord.writerecord("发送的命令为：" + chOprateCode);
                  ErrorRecord.writerecord("接收的数据为：" + rcvData);
                
                  Thread.Sleep(400);
              }
           }
     

             //前面已算校验，并完成错误处理，保证收到 了正确的数据，是否考虑在这里计算？
                for (int i = 0; i < 4; i++)                // 返回值，单位pls
                {
                    string strReturn;
                    strReturn = "" + (char)receivedData[4 + 2 * i];
                    strReturn += (char)receivedData[4 + 2 * i + 1];
                    pos_pls = (pos_pls << 8) + FormatValue.string2Int(strReturn);

                }
                pos = (pos_pls / scale);              
           

            return true;
        }

        public double getPosition_test()
        {

            byte[] receivedData = { 0x3E, 0x00, 0x01, (byte)'E', (byte)'0', (byte)'0', (byte)'0', (byte)'0', (byte)'3', (byte)'0', (byte)'3', (byte)'b' };
            int pos_pls = 0;
            if (receivedData != null)  //未算校验
            {
                for (int i = 0; i < 4; i++)                // 返回值，单位pls
                {
                    string strReturn = null;
                    strReturn += (char)receivedData[4 + 2 * i];
                    strReturn += (char)receivedData[4 + 2 * i + 1];
                    pos_pls = (pos_pls << 8) + FormatValue.string2Int(strReturn);

                }
                return (double)(pos_pls / scale);
            }
            else
                throw new ArgumentNullException("无数据null");
        }

        public void SetPosition_test()
        {
            ResetStartTime();
            ResetStartPos();
        }


       public  bool ArrivedPos_test()
        {
          
            do
            {
                Thread.Sleep(200);

            } while (!RuntimeTimeOut(500));

            return false;
        }


        /// <summary>
        /// 设置运动的位置,非阻塞，仅发送运动指令，不回读状态
        /// </summary>
        /// <param name="position_mm"></param>
      public void SetPosition(double position_mm)
      {
          int position_pls = (int)(position_mm * scale);

          if (position_pls < 0)
              position_pls = 0;
          if (position_pls > maxDistance_pls)
              position_pls = maxDistance_pls;

          const char chOprateCode = (char)ARM_CMD_CODE.POSITION;
          char chMotorAddr = (char)getArmAddr(armName);//  电机地址

          string strMsg = FormatValue.numToString(position_pls);
          char[] aMsg = strMsg.ToArray();
 //         isMsgEcho = false;
          SendMessage(chMotorAddr, chOprateCode, aMsg);

              ResetStartTime();
              ResetStartPos();
   

       }

       /// <summary>
      /// 设置运动的位置,阻塞，直到到达指定位置或错误才返回
       /// </summary>
       /// <param name="position_mm"></param>
      public bool SetPositionW(double position_mm)
      {
          int position_pls = (int)(position_mm * scale);

          if (position_pls < 0)
              position_pls = 0;
          if (position_pls > maxDistance_pls)
              position_pls = maxDistance_pls;

          ResetStartTime();
          ResetStartPos();

          const char chOprateCode = (char)ARM_CMD_CODE.POSITION;
          char chMotorAddr = (char)getArmAddr(armName);//  电机地址

          string strMsg = FormatValue.numToString(position_pls);
          char[] aMsg = strMsg.ToArray();

          SendMessage(chMotorAddr, chOprateCode, aMsg);
  

          ARM_ERROR_CODE code;
          if (!ArrivedPos(out code))
              return false;
          else
              return true;

      }

        //液面探测

        public bool detectLiquidLevel(double Hight=40,double Depth=0.13)
        {
            int probeDepth =(int) (Depth * scale);
            int probeHight = (int)(Hight * scale);
            const char chOprateCode = (char)ARM_CMD_CODE.LIQUIDLEVEL_DETECT;
            char chMotorAddr = (char)getArmAddr(armName);//  电机地址

            string strMsg = FormatValue.numToString(probeDepth);
            strMsg += FormatValue.numToString(probeHight);
            char[] aMsg = strMsg.ToArray();

            //         isMsgEcho = false;
            System.Diagnostics.Debug.Assert(haveProbe);
            if (haveProbe)
            {
                SendMessage(chMotorAddr, chOprateCode, aMsg);
            }
          

            ResetStartTime();
            ResetStartPos();
            return true;
        }

        /// <summary>
        /// 发送消息数据
        /// </summary>
        /// <param name="chMotorAddr"></param>
        /// <param name="chOprateCode"></param>
        /// <param name="aSendData"></param>
        private void SendMessage(char  chMotorAddr,  char chOprateCode,char[] abSendData=null)
        {
 //           SendMessage(chMotorAddr, chOprateCode);
           strReadySendMsg = null;

            //将地址转为ASCII码
           string strHex;
           strHex = FormatValue.Hex16ToString(chMotorAddr);

           strReadySendMsg += strHex;
            strReadySendMsg += chOprateCode;

            if (abSendData != null)
            {
                foreach (char ch in abSendData)
                {
                    strReadySendMsg += ch;
                }
            }
 
           byte[] aData= FramingSendData(strReadySendMsg);
          
            //发送数据
           int SendCnt = 0;
           int sendStatus;
           isMsgEcho = false;
           sendStatus = TransReceiveData(aData);  //发送是否成功？
           while ((sendStatus != 0)|| (!isMsgEcho))
          { 
               SendCnt++;
               if ((SendCnt > 20)  && (sendStatus != ERROR_CODE.CHECKERROR)) //连发10次之后，未成功，则提示是否重发,如果校验错误自动重发
               {
                   String msg = "《机械臂》串口通信故障！错误代码：" + sendStatus + "主板地址：" + armAddr + "检查COM口是否被占用，或请检查线路连接，然后重发";
                   ErrorRecord.writerecord(msg);
                   MessageBoxResult rt = MessageBox.Show(msg, "重发", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                   if (rt == MessageBoxResult.No)
                   {
                       break;
                   }
                   else if (rt == MessageBoxResult.Cancel)
                   {
                       break;
                   }
                   else
                   {
                       SendCnt = 0;
                   }
               }
               isMsgEcho = false;
               sendStatus = TransReceiveData(aData);
 
               Thread.Sleep(200);
          }

           if (sendStatus != 0)
           {
   //            DisPlayMsg("信息发送失败");
               string strError = "其他未知错误";
               if (sendStatus == ERROR_CODE.SENDFAIL)
                   strError = "信息发送失败";
               if (sendStatus == ERROR_CODE.NOANSWER)
                   strError = "无应答";

               throw new HardwareException("机械臂通信COM串口通信错误", ErrorHardware.ArmCom, strError);
           }

  
        }
        /// <summary>
        /// 发送，并接收应答
        /// 0  ok,1 noanswer,2 sendfail
        /// </summary>
        /// <param name="aData"></param>
        /// <returns></returns>
        private int TransReceiveData(byte[] aData)
        {
            //发送数据
            com.ClearBuffer();
       
            if (com.ComDataSend(aData))
            {
                string strSend = null;
                //int decNum = 0;//存储十进制
                for (int i = 0; i < aData.Length; i++) //窗体显示
                {
                    strSend += (char)aData[i];

                }

                //发送数据事件
                if (this.armSend != null)
                {
                    this.armSend(strSend);
                }

                //定时读取返回的数据
                //              startTimer();
                //              DisplaySendMsg(aData);
                Thread.Sleep(60);
                receivedData = com.ReadReceivedData();
  
                if (receivedData != null)  //收到返回信息
                {
                    if (VerifyData(receivedData))
                    {
                        isMsgEcho = true;
                        refreshData(receivedData);
                    }
                    else  //校验错误
                    {
                        return ERROR_CODE.CHECKERROR;
                    }

                }
                else
                {

                    return ERROR_CODE.SENDFAIL;
                }
            }
            else
            {
                return ERROR_CODE.NOANSWER;
            }
            return ERROR_CODE.NOERROR;
        }

        private bool VerifyData(byte[] receivedData)
        {
            bool isRight = true;
            int Length = receivedData.Length;

            int validataLen = Length - 6;
            if (validataLen < 0)
            {
                ErrorRecord.writerecord("机械臂接受数据长度错误:" + Length+ "个字节\r\n");
                return false;
            }
  /*          byte[] validateData = new byte[validataLen];  //去除4个CRC和2个帧尾

            for (int index = 0; index < validataLen;index++ )
            {
                validateData[index] = receivedData[index];
            }
*/
            ushort check;
            check = cal_crc(receivedData, validataLen);

            char[] char2 = new char[2];
            char2[0] = (char)receivedData[validataLen]; //CRC校验
            char2[1] = (char)receivedData[validataLen+1];  
            uint checkH = FormatValue.charToHex16(char2);

            char2[0] = (char)receivedData[validataLen + 2];
            char2[1] = (char)receivedData[validataLen + 3]; 
            uint checkL = FormatValue.charToHex16(char2);


            ushort recvCheck = (ushort)(checkH * 256 + checkL);

            if (check == recvCheck)
                isRight = true;
            else
                isRight = false;

            return isRight;

        }

        /// <summary>
        /// 发送数据组帧 
        /// 增加帧头、帧尾和校验
        /// </summary>
        public byte[] FramingSendData(string strReadySendMsg)
        {
            
            //str 转 byte
            byte[] abData = System.Text.Encoding.Default.GetBytes(strReadySendMsg);
            
            //
             int iNumSendData = abData.Length;
             byte[] aSendData = new byte[iNumSendData + 7];  //发送数据数量 4个字符的校验 2个字符的帧尾，一个帧头

            aSendData[0] = FRAME_HEAD_Arm484;
            for (int i = 0; i < iNumSendData; i++)
            {
                aSendData[i + 1] = abData[i];
            }

            //计算CRC
            UInt16 uCheckCRC;

            // uCheckCRC = cal_crc(crcData, 3);
            uCheckCRC = cal_crc(aSendData, iNumSendData+1);
            //crc高位
            byte bHgihtCRC;
            string strHex;
            bHgihtCRC = Convert.ToByte(uCheckCRC >> 8);
            strHex = FormatValue.Hex16ToString(bHgihtCRC);
            aSendData[iNumSendData+1] = (byte)strHex[0];
            aSendData[iNumSendData+2] = (byte)strHex[1];

            //crc低位
            byte bLowCRC;
            bLowCRC = Convert.ToByte((uCheckCRC) & (0x00FF));
            strHex = FormatValue.Hex16ToString(bLowCRC);
            aSendData[iNumSendData + 3] = (byte)strHex[0];
            aSendData[iNumSendData + 4] = (byte)strHex[1];

            aSendData[iNumSendData + 5] = (byte)(0x0D);
            aSendData[iNumSendData + 6] = (byte)(0x0A);

            return aSendData;
        }


        /// <summary>
        /// 接收，更新数据
        /// </summary>
        /// <param name="receivedData"></param>
        private void refreshData(byte[] receivedData)
        {
            string strData = null;
            if (receivedData != null)
            {
                for (int i = 0; i < receivedData.Length; i++) 
                {
                    strData += (char)receivedData[i];    //以字符显示
                 }
            }


            //接收数据事件
           if (this.armReceive != null)
            {
                this.armReceive(strData);
            }

        }

        /// <summary>
        /// 开始定时器
        /// </summary>
        private void startTimer()
        {
  //          throw new NotImplementedException();
  /*        this.ticker = new DispatcherTimer();
            this.ticker.Tick += this.OnTimedEvent;
            this.ticker.Interval = new TimeSpan(0, 0, 1); // 1 second
            this.ticker.Start();
   * */
            System.Timers.Timer t = new System.Timers.Timer(200);
            t.Elapsed += new System.Timers.ElapsedEventHandler(OnTimedEvent);
            t.AutoReset = false;
            t.Enabled = true;
        }

        /// <summary>
        /// 定时器响应函数
        /// </summary>
        /// <param name="source"></param>
        /// <param name="args"></param>
        private void OnTimedEvent(object source, EventArgs args)
        {
            receivedData = com.ReadReceivedData();
            isMsgEcho = true;
 //           if(receivedData != null)  //收到返回信息
              refreshData(receivedData);
        }

   
        /// <summary>
        /// crc 校验  
        /// <param name="data"></param>
        /// <param name="nbyte"></param>
        /// <returns></returns>
        UInt16 cal_crc(byte[] data, int nbyte)
        {
            UInt16 itemp = 0xFFFF;
            byte i;
            byte count;
            for (count = 0; count < nbyte; count++)
            {

                itemp ^= data[count];

                for (i = 0; i < 8; i++)
                {
                    if ((itemp & 0x0001) != 0)
                    {
                        itemp >>= 1;
                        itemp ^= 0xA001;
                    }
                    else
                    {
                        itemp >>= 1;
                    }
                }
            }
            return itemp;
        }

//
        /// <summary>
        /// 等待到达指定位置,如果出错，返回错误信息。
        /// 0为正在运行，1为到达目标，2异常撞击 ，3检测到液面 4未检测到液面
        ///5未检测到原点  7左原点极限    8右原点极限  
        /// </summary>
        /// <param name="errorCode"></param>
        /// <returns></returns>
        internal bool ArrivedPos(out ARM_ERROR_CODE errorCode)
        {
 //           throw new NotImplementedException();
            ARM_CMD_CODE code = ARM_CMD_CODE.POSITION_QUERY;
            errorCode = readInf(code);

            while (ARM_ERROR_CODE.RUNNING == errorCode) 
            {
                Thread.Sleep(200);    
                errorCode = readInf(code);

                if (RuntimeTimeOut(3000))   //撞针时需要
                {
                    errorCode = ARM_ERROR_CODE.UNKNOW_ERROR;
                    Stop();
                    if ((ArmName == "ArmZ1") || (ArmName == "ArmZ2"))
                    {
                        Reset();
                    }
                    return false;
                }
             } 

            if ((errorCode != ARM_ERROR_CODE.ARRIVED) && (errorCode != ARM_ERROR_CODE.DETECTED))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        static private DateTime startTime;
        private void ResetStartTime()
        {
            startTime = DateTime.Now;
        }

/// <summary>
/// 
/// </summary>
        static private double startPos;
        private bool ResetStartPos()
        {
            if (!getPosition(out startPos))
            {
                return false;
            }
            return true;
        }



         private   const double deltaPos = 4;
/// <summary>
/// 超时 为 true
/// 在一定时间内，移动的距离不够即认为超时异常
/// </summary>
/// <param name="mTime"></param>
/// <returns></returns>
        private bool RuntimeTimeOut(int mTime)
        {
  //          throw new NotImplementedException();
            TimeSpan span = DateTime.Now - startTime;

            if (span.TotalMilliseconds > mTime)
            {
                startTime = DateTime.Now;

               double currentPos;
                getPosition(out currentPos);
                double dePos = (currentPos - startPos);
                DailyRecord.writerecord("运动超时异常" + dePos.ToString("F2") + "mm" + " 超时：" + span.ToString());
                if (Math.Abs(currentPos - startPos) < deltaPos)
                {

                    return true;   
                }

                startPos = currentPos;
                return false;

            }
            return false ;

        }
/// <summary>
/// 等待复位完成，如果出现异常，返回错误代码
/// </summary>
/// <returns></returns>
        internal ARM_ERROR_CODE QueryReset()
        {
  //          throw new NotImplementedException();
            ARM_ERROR_CODE errorCode;
            ARM_CMD_CODE code = ARM_CMD_CODE.RESET_QUERY;
            do
            {
                errorCode = readInf(code);
   
                Thread.Sleep(200);
            } while ((ARM_ERROR_CODE.RUNNING == errorCode) );

            if (errorCode != ARM_ERROR_CODE.ARRIVED)
            {
                Stop();
  //              DisPlayMsg("移动失败,故障代码：" + errorCode);

  //              throw new HardwareException(armName + "复位失败,故障代码：" + errorCode, ErrorHardware.Arm, "硬件故障");              
            }
            return errorCode;
        }

        internal void Stop()
        {
    //        throw new NotImplementedException();
            const char chOprateCode = (char)ARM_CMD_CODE.STOP;
            char chMotorAddr = (char)getArmAddr(armName);//  电机地址
            char[] para = { '0' };
            SendMessage(chMotorAddr, chOprateCode, para);
          
        }

        private void DisPlayMsg(string msg)
        {
            //           throw new NotImplementedException();
            msg = armName + msg;
            DailyRecord.writerecord(msg);
            MessageBox.Show(msg);
        }
    }
}
