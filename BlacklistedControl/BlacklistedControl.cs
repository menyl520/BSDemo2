using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Drawing;
using System.IO;

namespace Suprema
{
    public class BlacklistedControl : FunctionModule
    {
        protected override List<KeyValuePair<string, Action<IntPtr, UInt32, bool>>> getFunctionList(IntPtr sdkContext, UInt32 deviceID, bool isMasterDevice)
        {
            List<KeyValuePair<string, Action<IntPtr, UInt32, bool>>> functionList = new List<KeyValuePair<string, Action<IntPtr, uint, bool>>>();
            functionList.Add(new KeyValuePair<string, Action<IntPtr, uint, bool>>("Get access group", getAccessGroup));
            functionList.Add(new KeyValuePair<string, Action<IntPtr, uint, bool>>("Set access settings", InitAccessGroup));
            functionList.Add(new KeyValuePair<string, Action<IntPtr, uint, bool>>("Add blacklisted user", AddBlacklistedUser));          

            return functionList;
        }

        public void getAccessGroup(IntPtr sdkContext, uint deviceID, bool isMasterDevice)
        {
            IntPtr accessGroupObj = IntPtr.Zero;
            UInt32 numAccessGroup = 0;
            BS2ErrorCode result = BS2ErrorCode.BS_SDK_SUCCESS;

            Console.WriteLine("Do you want to get all access groups? [Y/n]");
            Console.Write(">>>> ");
            if (Util.IsYes())
            {
                Console.WriteLine("Trying to get all access gruops from device.");
                result = (BS2ErrorCode)API.BS2_GetAllAccessGroup(sdkContext, deviceID, out accessGroupObj, out numAccessGroup);
            }
            else
            {
                Console.WriteLine("Enter the ID of the access group which you want to get: [ID_1,ID_2 ...]");
                Console.Write(">>>> ");
                char[] delimiterChars = { ' ', ',', '.', ':', '\t' };
                string[] accessGroupIDs = Console.ReadLine().Split(delimiterChars);
                List<UInt32> accessGroupIDList = new List<UInt32>();

                foreach (string accessGroupID in accessGroupIDs)
                {
                    if (accessGroupID.Length > 0)
                    {
                        UInt32 item;
                        if (UInt32.TryParse(accessGroupID, out item))
                        {
                            accessGroupIDList.Add(item);
                        }
                    }
                }

                if (accessGroupIDList.Count > 0)
                {
                    IntPtr accessGroupIDObj = Marshal.AllocHGlobal(4 * accessGroupIDList.Count);
                    IntPtr curAccessGroupIDObj = accessGroupIDObj;
                    foreach (UInt32 item in accessGroupIDList)
                    {
                        Marshal.WriteInt32(curAccessGroupIDObj, (Int32)item);
                        curAccessGroupIDObj = (IntPtr)((long)curAccessGroupIDObj + 4);
                    }

                    Console.WriteLine("Trying to get access gruops from device.");
                    result = (BS2ErrorCode)API.BS2_GetAccessGroup(sdkContext, deviceID, accessGroupIDObj, (UInt32)accessGroupIDList.Count, out accessGroupObj, out numAccessGroup);

                    Marshal.FreeHGlobal(accessGroupIDObj);
                }
                else
                {
                    Console.WriteLine("Invalid parameter");
                }
            }

            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("Got error({0}).", result);
            }
            else if (numAccessGroup > 0)
            {
                IntPtr curAccessGroupObj = accessGroupObj;
                int structSize = Marshal.SizeOf(typeof(BS2AccessGroup));

                for (int idx = 0; idx < numAccessGroup; ++idx)
                {
                    BS2AccessGroup item = (BS2AccessGroup)Marshal.PtrToStructure(curAccessGroupObj, typeof(BS2AccessGroup));
                    print(sdkContext, item);
                    curAccessGroupObj = (IntPtr)((long)curAccessGroupObj + structSize);
                }

                API.BS2_ReleaseObject(accessGroupObj);
            }
            else
            {
                Console.WriteLine(">>> There is no access group in the device.");
            }
        }

        public void InitAccessGroup(IntPtr sdkContext, uint deviceID, bool isMasterDevice)
        {
            bool go = false;
            IntPtr accessGroupIDObj = Marshal.AllocHGlobal(4);
            IntPtr curAccessGroupIDObj = accessGroupIDObj;
            IntPtr accessGroupObj = IntPtr.Zero;
            UInt32 numAccessGroup = 0;
            uint maxGroup = 3;
            Marshal.WriteInt32(curAccessGroupIDObj, (Int32)1);
            curAccessGroupIDObj = (IntPtr)((long)curAccessGroupIDObj + 4);

            Console.WriteLine("Trying to get access groups from device.");
            BS2ErrorCode result = (BS2ErrorCode)API.BS2_GetAccessGroup(sdkContext, deviceID, accessGroupIDObj, (UInt32)maxGroup, out accessGroupObj, out numAccessGroup);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("GetAccessGroup error({0}).", result);
            }
            else
            {
                if (numAccessGroup != maxGroup) go = true;
                else if (numAccessGroup > 0)
                {
                    API.BS2_ReleaseObject(accessGroupObj);
                }
            }
            Marshal.FreeHGlobal(accessGroupIDObj);

            if (go)
            {
                string[] accessGroupNames = new string[] { "FreeAccess", "VisitorFreeAccess", "BlacklistedAccess" };
                byte[] accessGroupArray = null;
                List<BS2AccessGroup> accessGroupList = new List<BS2AccessGroup>();

                for (uint i = 1; i <= maxGroup; i++)
                {
                    BS2AccessGroup accessGroup = Util.AllocateStructure<BS2AccessGroup>();
                    accessGroup.id = i; //1

                    Array.Clear(accessGroup.name, 0, BS2Environment.BS2_MAX_ACCESS_GROUP_NAME_LEN);
                    accessGroupArray = Encoding.UTF8.GetBytes(accessGroupNames[i - 1]);
                    Array.Copy(accessGroupArray, accessGroup.name, accessGroupArray.Length);

                    accessGroup.numOflevelUnion.numAccessLevels = 0;
                    accessGroup.levelUnion.accessLevels[accessGroup.numOflevelUnion.numAccessLevels++] = i;
                    accessGroupList.Add(accessGroup);
                }

                int structSize = Marshal.SizeOf(typeof(BS2AccessGroup));
                IntPtr accessGroupListObj = Marshal.AllocHGlobal(structSize * accessGroupList.Count);
                IntPtr curAccessGroupListObj = accessGroupListObj;
                foreach (BS2AccessGroup item in accessGroupList)
                {
                    Marshal.StructureToPtr(item, curAccessGroupListObj, false);
                    curAccessGroupListObj = (IntPtr)((long)curAccessGroupListObj + structSize);
                }

                Console.WriteLine("Trying to set access groups to device.");
                result = (BS2ErrorCode)API.BS2_SetAccessGroup(sdkContext, deviceID, accessGroupListObj, (UInt32)accessGroupList.Count);
                if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                {
                    Console.WriteLine("Got error({0}).", result);
                }

                Marshal.FreeHGlobal(accessGroupListObj);

                if (result == BS2ErrorCode.BS_SDK_SUCCESS)
                {
                    List<BS2AccessLevel> accessLevelList = new List<BS2AccessLevel>();
                    for (uint i = 1; i <= maxGroup; i++)
                    {
                        BS2AccessLevel accessLevel = Util.AllocateStructure<BS2AccessLevel>();
                        accessLevel.id = i;
                        Array.Clear(accessLevel.name, 0, BS2Environment.BS2_MAX_ACCESS_GROUP_NAME_LEN);
                        accessGroupArray = Encoding.UTF8.GetBytes(accessGroupNames[i - 1]);
                        Array.Copy(accessGroupArray, accessLevel.name, accessGroupArray.Length);
                        if (i == maxGroup)
                        {
                            accessLevel.numDoorSchedules = 1;
                            accessLevel.doorSchedules[0].doorID = 1;
                            accessLevel.doorSchedules[0].scheduleID = 0;
                        }
                        else
                        {
                            accessLevel.numDoorSchedules = 1;
                            accessLevel.doorSchedules[0].doorID = 1;
                            accessLevel.doorSchedules[0].scheduleID = 1;
                        }
                        accessLevelList.Add(accessLevel);
                    }

                    structSize = Marshal.SizeOf(typeof(BS2AccessLevel));
                    IntPtr accessLevelListObj = Marshal.AllocHGlobal(structSize * accessLevelList.Count);
                    IntPtr curAccessLevelListObj = accessLevelListObj;
                    foreach (BS2AccessLevel item in accessLevelList)
                    {
                        Marshal.StructureToPtr(item, curAccessLevelListObj, false);
                        curAccessLevelListObj = (IntPtr)((long)curAccessLevelListObj + structSize);
                    }

                    Console.WriteLine("Trying to set access levels to device.");
                    result = (BS2ErrorCode)API.BS2_SetAccessLevel(sdkContext, deviceID, accessLevelListObj, (UInt32)accessLevelList.Count);
                    if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                    {
                        Console.WriteLine("Got error({0}).", result);
                    }

                    Marshal.FreeHGlobal(accessLevelListObj);

                    if (result == BS2ErrorCode.BS_SDK_SUCCESS)
                    {
                        List<CSP_BS2Schedule> accessScheduleList = new List<CSP_BS2Schedule>();

                        #region FreeAccess

                        //CSP_BS2Schedule freeAccessSchedule = Util.AllocateStructure<CSP_BS2Schedule>();
                        //freeAccessSchedule.id = 1;
                        //byte[] accessScheduleArray = Encoding.UTF8.GetBytes("Always");
                        //Array.Clear(freeAccessSchedule.name, 0, BS2Environment.BS2_MAX_SCHEDULE_NAME_LEN);
                        //Array.Copy(accessScheduleArray, freeAccessSchedule.name, accessScheduleArray.Length);
                        //freeAccessSchedule.numHolidaySchedules = 0;
                        //freeAccessSchedule.isDaily = 1;

                        //freeAccessSchedule.scheduleUnion.daily.startDate = Convert.ToUInt32(Util.ConvertToUnixTimestamp(new DateTime(2023, 1, 1)));
                        //freeAccessSchedule.scheduleUnion.daily.numDays = 7;
                        //for (byte loop = 0; loop < freeAccessSchedule.scheduleUnion.daily.numDays; ++loop)
                        //{
                        //    freeAccessSchedule.scheduleUnion.daily.schedule[loop].numPeriods = (byte)1;

                        //    for (byte z = 0; z < freeAccessSchedule.scheduleUnion.daily.schedule[loop].numPeriods; ++z)
                        //    {
                        //        freeAccessSchedule.scheduleUnion.daily.schedule[loop].periods[z].startTime = (UInt16)(60 * Convert.ToUInt16("00") + Convert.ToUInt16("00"));
                        //        freeAccessSchedule.scheduleUnion.daily.schedule[loop].periods[z].endTime = (UInt16)(60 * Convert.ToUInt16("23") + Convert.ToUInt16("59"));
                        //    }
                        //}
                        //accessScheduleList.Add(freeAccessSchedule);

                        #endregion

                        #region NoAccess

                        CSP_BS2Schedule noAccessSchedule = Util.AllocateStructure<CSP_BS2Schedule>();
                        noAccessSchedule.id = 0;
                        byte[] noAccessScheduleArray = Encoding.UTF8.GetBytes("NoAccess");
                        Array.Clear(noAccessSchedule.name, 0, BS2Environment.BS2_MAX_SCHEDULE_NAME_LEN);
                        Array.Copy(noAccessScheduleArray, noAccessSchedule.name, noAccessScheduleArray.Length);

                        noAccessSchedule.numHolidaySchedules = 0;
                        noAccessSchedule.isDaily = 0x01;
                        noAccessSchedule.scheduleUnion.daily.startDate = Convert.ToUInt32(Util.ConvertToUnixTimestamp(new DateTime(2022, 12, 31)));
                        noAccessSchedule.scheduleUnion.daily.numDays = 7;
                        for (byte loop = 0; loop < noAccessSchedule.scheduleUnion.daily.numDays; ++loop)
                        {
                            noAccessSchedule.scheduleUnion.daily.schedule[loop].numPeriods = (byte)1;
                            for (byte z = 0; z < noAccessSchedule.scheduleUnion.daily.schedule[loop].numPeriods; ++z)
                            {
                                noAccessSchedule.scheduleUnion.daily.schedule[loop].periods[z].startTime = (UInt16)(60 * Convert.ToUInt16("00") + Convert.ToUInt16("00"));
                                noAccessSchedule.scheduleUnion.daily.schedule[loop].periods[z].endTime = (UInt16)(60 * Convert.ToUInt16("00") + Convert.ToUInt16("01"));
                            }
                        }
                        accessScheduleList.Add(noAccessSchedule);

                        #endregion 

                        result = (BS2ErrorCode)API.CSP_BS2_SetAccessSchedule(sdkContext, deviceID, accessScheduleList.ToArray(), (UInt32)accessScheduleList.Count);
                        if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                        {
                            Console.WriteLine("SetAccessSchedule error({0}).", result);
                        }
                    }
                }
            }
        }
        
        public void AddBlacklistedUser(IntPtr sdkContext, uint deviceID, bool isMasterDevice)
        {
            BS2UserFaceExBlob[] userBlobs = Util.AllocateStructureArray<BS2UserFaceExBlob>(1);
            BS2UserFaceExBlob userFaceBlob = Util.AllocateStructure<BS2UserFaceExBlob>();
            BS2UserBlob userBlob = Util.AllocateStructure<BS2UserBlob>();
            BS2User user = new BS2User();
            string userID = "BL123";
            string name = "Test Blacklisted";
            string imagePath = @"C:\Users\YeeLing\Desktop\Potrait\YeeLing.png";

            // Load the image into a byte array
            byte[] imageData = File.ReadAllBytes(imagePath);

            // Convert the byte array to a base64-encoded string
            string base64PhotoString = Convert.ToBase64String(imageData);

            userBlob.user = user;

            userBlob.user.version = 0;
            userBlob.user.formatVersion = 0;
            userBlob.user.faceChecksum = 0;
            //UserBlob.user.fingerChecksum = 0;
            userBlob.user.authGroupID = 0;
            userBlob.user.numCards = 0;
            userBlob.user.numFingers = 0;
            userBlob.user.numFaces = 0;
            userBlob.user.flag = 0;

            userBlob.cardObjs = IntPtr.Zero;
            userBlob.fingerObjs = IntPtr.Zero;
            userBlob.faceObjs = IntPtr.Zero;
                        
            byte[] userIDArray = Encoding.UTF8.GetBytes(userID);
            userBlob.user.userID = new byte[BS2Environment.BS2_USER_ID_SIZE];
            Array.Copy(userIDArray, userBlob.user.userID, userIDArray.Length);

            userBlob.setting.idAuthMode = (byte)BS2IDAuthModeEnum.NONE;
            userBlob.setting.cardAuthMode = (byte)BS2CardAuthModeEnum.NONE;
            userBlob.setting.fingerAuthMode = (byte)BS2FingerAuthModeEnum.NONE;

            userBlob.setting.fingerAuthMode = (byte)BS2FingerAuthModeEnum.BIOMETRIC_ONLY;
            userBlob.setting.idAuthMode = (byte)BS2IDAuthModeEnum.PROHIBITED;
            userBlob.setting.cardAuthMode = (byte)BS2CardAuthModeEnum.NONE;

            userBlob.setting.securityLevel = (byte)0;

            Array.Clear(userBlob.name, 0, BS2Environment.BS2_USER_NAME_LEN);
            if (Convert.ToBoolean(deviceInfo.userNameSupported))
            {
                if (string.IsNullOrWhiteSpace(name) || ((!string.IsNullOrWhiteSpace(name)) && (name.Length == 0)))
                {
                    Console.WriteLine("[Warning] user name will be displayed as empty.");
                }
                else
                {
                    if (name.Length > BS2Environment.BS2_USER_NAME_LEN)
                    {
                        name = name.Substring(0, BS2Environment.BS2_USER_NAME_LEN);
                    }
                    byte[] userNameArray = Encoding.UTF8.GetBytes(name);
                    Array.Copy(userNameArray, userBlob.name, userNameArray.Length);
                }
            }

            Array.Clear(userBlob.pin, 0, BS2Environment.BS2_PIN_HASH_SIZE);
            
            userBlob.photo.size = 0;
            Array.Clear(userBlob.photo.data, 0, BS2Environment.BS2_USER_PHOTO_SIZE);
         
            Array.Clear(userBlob.accessGroupId, 0, BS2Environment.BS2_MAX_ACCESS_GROUP_PER_USER);           
            userBlob.accessGroupId[0] = Convert.ToUInt32(3);

            //BS2UserFaceExBlob userFaceBlob;
            userFaceBlob = Util.AllocateStructure<BS2UserFaceExBlob>();
            userFaceBlob.user = userBlob.user;
            userFaceBlob.setting = userBlob.setting;
            userFaceBlob.name = userBlob.name;
            userFaceBlob.pin = userBlob.pin;
            userFaceBlob.cardObjs = userBlob.cardObjs;
            userFaceBlob.fingerObjs = userBlob.fingerObjs;
            userFaceBlob.faceObjs = userBlob.faceObjs;
            userFaceBlob.accessGroupId = userBlob.accessGroupId;

            userFaceBlob.user_photo_obj = IntPtr.Zero;
            userFaceBlob.faceExObjs = IntPtr.Zero;

            if (!string.IsNullOrWhiteSpace(base64PhotoString))
            {
                Image faceImage = null;
                try
                {                    
                    MemoryStream ms = new MemoryStream(imageData, 0, imageData.Length);
                    ms.Write(imageData, 0, imageData.Length);
                    faceImage = Image.FromStream(ms, true);//Exception occurs here
                    if (!faceImage.RawFormat.Equals(System.Drawing.Imaging.ImageFormat.Jpeg))
                    {
                        Console.WriteLine("Invalid image file format");
                    }
                    else
                    {
                        IntPtr imageDataPtr = IntPtr.Zero;
                        UInt32 imageLen = 0;

                        imageDataPtr = Marshal.AllocHGlobal(imageData.Length);
                        Marshal.Copy(imageData, 0, imageDataPtr, imageData.Length);
                        imageLen = (UInt32)imageData.Length;
                        int structHeaderSize = Marshal.SizeOf(typeof(BS2FaceExUnwarped));
                        int totalSize = structHeaderSize + (int)imageLen;
                        userFaceBlob.faceExObjs = Marshal.AllocHGlobal(totalSize);
                        IntPtr curFaceExObjs = userFaceBlob.faceExObjs;

                        BS2FaceExUnwarped unwarped = Util.AllocateStructure<BS2FaceExUnwarped>();
                        unwarped.flag = 0;
                        unwarped.imageLen = imageLen;

                        Marshal.StructureToPtr(unwarped, curFaceExObjs, false);
                        curFaceExObjs += structHeaderSize;
                        Util.CopyMemory(curFaceExObjs, imageDataPtr, imageLen);
                        userFaceBlob.user.numFaces = 1;
                    }
                }
                catch { }
            }
           
            userFaceBlob.settingEx.faceAuthMode = (byte)BS2ExtFaceAuthModeEnum.NONE;
            userFaceBlob.settingEx.fingerprintAuthMode = (byte)BS2ExtFingerprintAuthModeEnum.NONE;
            userFaceBlob.settingEx.cardAuthMode = (byte)BS2ExtCardAuthModeEnum.NONE;
            userFaceBlob.settingEx.idAuthMode = (byte)BS2ExtIDAuthModeEnum.NONE;
            
            userBlobs[0] = userFaceBlob;

            Console.WriteLine("Trying to enrol user {0} to {1}", Encoding.UTF8.GetString(userBlobs[0].user.userID), deviceID);
            BS2ErrorCode result = (BS2ErrorCode)API.BS2_EnrollUserFaceEx(sdkContext, deviceID, userBlobs, 1, 1);

            foreach (BS2UserFaceExBlob curretUserBlob in userBlobs)
            {
                if (curretUserBlob.cardObjs != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(curretUserBlob.cardObjs);
                }

                if (curretUserBlob.fingerObjs != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(curretUserBlob.fingerObjs);
                }

                if (curretUserBlob.faceObjs != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(curretUserBlob.faceObjs);
                }
                if (curretUserBlob.faceExObjs != IntPtr.Zero)
                {
                    //if (unwarpedMemory)
                    Marshal.FreeHGlobal(curretUserBlob.faceExObjs);
                }
            }
        }

        void print(IntPtr sdkContext, BS2AccessGroup accessGroup)
        {
            Console.WriteLine(">>>> AccessGroup id[{0}] name[{1}]", accessGroup.id, Encoding.UTF8.GetString(accessGroup.name).TrimEnd('\0'));
            Console.WriteLine("     |--accessLevels");
            for (byte loop = 0; loop < accessGroup.numOflevelUnion.numAccessLevels; ++loop)
            {
                Console.WriteLine("     |  |--accessLevelID[{0}]", accessGroup.levelUnion.accessLevels[loop]);
            }
        }

        void print(IntPtr sdkContext, BS2AccessLevel accessLevel)
        {
            Console.WriteLine(">>>> AccessLevel id[{0}] name[{1}]", accessLevel.id, Encoding.UTF8.GetString(accessLevel.name).TrimEnd('\0'));
            Console.WriteLine("     |--doorSchedules");
            for (byte loop = 0; loop < accessLevel.numDoorSchedules; ++loop)
            {
                Console.WriteLine("     |  |--doorID[{0}] scheduleID[{1}]", accessLevel.doorSchedules[loop].doorID, accessLevel.doorSchedules[loop].scheduleID);
            }
        }

        void print(IntPtr sdkContext, CSP_BS2Schedule schedule)
        {
            Console.WriteLine(">>>> Schedule id[{0}] name[{1}]", schedule.id, Encoding.UTF8.GetString(schedule.name).TrimEnd('\0'));
            if (Convert.ToBoolean(schedule.isDaily))
            {
                Console.WriteLine("     |--dailySchedules startDate[{0}] numDays[{1}]", Util.ConvertFromUnixTimestamp(schedule.scheduleUnion.daily.startDate).ToString("yyyy-MM-dd HH:mm:ss"), schedule.scheduleUnion.daily.numDays);
                for (byte loop = 0; loop < schedule.scheduleUnion.daily.numDays; ++loop)
                {
                    Console.Write("     |  |--schedule[{0, 2}] [", loop);
                    for (byte z = 0; z < schedule.scheduleUnion.daily.schedule[loop].numPeriods; ++z)
                    {
                        UInt32 startTime = (UInt32)60 * schedule.scheduleUnion.daily.schedule[loop].periods[z].startTime;
                        UInt32 endTime = (UInt32)60 * schedule.scheduleUnion.daily.schedule[loop].periods[z].endTime;

                        if (z + 1 < schedule.scheduleUnion.daily.schedule[loop].numPeriods)
                        {
                            Console.Write("{0}-{1}, ", Util.ConvertFromUnixTimestamp(startTime).ToString("HH:mm"), Util.ConvertFromUnixTimestamp(endTime).ToString("HH:mm"));
                        }
                        else
                        {
                            Console.Write("{0}-{1}", Util.ConvertFromUnixTimestamp(startTime).ToString("HH:mm"), Util.ConvertFromUnixTimestamp(endTime).ToString("HH:mm"));
                        }
                    }
                    Console.WriteLine("]");
                }
            }
            else
            {
                Console.WriteLine("     |--weeklySchedules");
                for (byte loop = 0; loop < BS2Environment.BS2_NUM_WEEKDAYS; ++loop)
                {
                    Console.Write("     |  |--schedule[{0, 10}] [", ((DayOfWeek)loop).ToString());
                    for (byte z = 0; z < schedule.scheduleUnion.weekly.schedule[loop].numPeriods; ++z)
                    {
                        UInt32 startTime = (UInt32)60 * schedule.scheduleUnion.weekly.schedule[loop].periods[z].startTime;
                        UInt32 endTime = (UInt32)60 * schedule.scheduleUnion.weekly.schedule[loop].periods[z].endTime;

                        if (z + 1 < schedule.scheduleUnion.weekly.schedule[loop].numPeriods)
                        {
                            Console.Write("{0}-{1}, ", Util.ConvertFromUnixTimestamp(startTime).ToString("HH:mm"), Util.ConvertFromUnixTimestamp(endTime).ToString("HH:mm"));
                        }
                        else
                        {
                            Console.Write("{0}-{1}", Util.ConvertFromUnixTimestamp(startTime).ToString("HH:mm"), Util.ConvertFromUnixTimestamp(endTime).ToString("HH:mm"));
                        }
                    }
                    Console.WriteLine("]");
                }
            }

            Console.WriteLine("     |--holidaySchedules numDays[{0}]", schedule.numHolidaySchedules);
            for (byte loop = 0; loop < schedule.numHolidaySchedules; ++loop)
            {
                Console.Write("     |  |-- id[{0}] schedule[", schedule.holidaySchedules[loop].id);

                for (byte z = 0; z < schedule.holidaySchedules[loop].schedule.numPeriods; ++z)
                {
                    UInt32 startTime = (UInt32)60 * schedule.holidaySchedules[loop].schedule.periods[z].startTime;
                    UInt32 endTime = (UInt32)60 * schedule.holidaySchedules[loop].schedule.periods[z].endTime;

                    if (z + 1 < schedule.holidaySchedules[loop].schedule.numPeriods)
                    {
                        Console.Write("{0}-{1}, ", Util.ConvertFromUnixTimestamp(startTime).ToString("HH:mm"), Util.ConvertFromUnixTimestamp(endTime).ToString("HH:mm"));
                    }
                    else
                    {
                        Console.Write("{0}-{1}", Util.ConvertFromUnixTimestamp(startTime).ToString("HH:mm"), Util.ConvertFromUnixTimestamp(endTime).ToString("HH:mm"));
                    }
                }
                Console.WriteLine("]");
            }
        }

        void print(IntPtr sdkContext, BS2HolidayGroup holidayGroup)
        {
            Console.WriteLine(">>>> HolidayGroup id[{0}] name[{1}]", holidayGroup.id, Encoding.UTF8.GetString(holidayGroup.name).TrimEnd('\0'));
            Console.WriteLine("     |--holidays");
            for (byte loop = 0; loop < holidayGroup.numHolidays; ++loop)
            {
                Console.WriteLine("     |  |--date[{0}] recurrence[{1}]", Util.ConvertFromUnixTimestamp(holidayGroup.holidays[loop].date).ToString("yyyy-MM-dd"), holidayGroup.holidays[loop].recurrence);
            }
        }
    }
}
