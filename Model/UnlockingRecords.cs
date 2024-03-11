using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JCScanhubClockInSystem.Model
{
    public class RootObject
    {
        public int Found { get; set; }
        public List<UnlockingRecords> Records { get; set; }
    }
    public class UnlockingRecords
    {
        public int? AttendanceState { get; set; }
        public string CardName { get; set; }
        public string CardNo { get; set; }
        public int? CardType { get; set; }
        public long CreateTime { get; set; }
        public int? Door { get; set; }
        public int? ErrorCode { get; set; }
        public int? FaceIndex { get; set; }
        public string HatColor { get; set; }
        public int? HatType { get; set; }
        public int? Mask { get; set; }
        public int? Method { get; set; }
        public string Notes { get; set; }
        public string Password { get; set; }
        public int? ReaderID { get; set; }
        public int? RecNo { get; set; }
        public int? RemainingTimes { get; set; }
        public int? ReservedInt { get; set; }
        public string ReservedString { get; set; }
        public string RoomNumber { get; set; }
        public int? Status { get; set; }
        public string Type { get; set; }
        public string URL { get; set; }
        public string UserID { get; set; }
        public int? UserType { get; set; }
        public string IPAddress { get; set; }
        public string SerialNo { get; set; }
        public string DeviceModel { get; set; }
    }
}
