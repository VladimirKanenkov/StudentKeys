using System;

namespace MyClient
{
    [Serializable]
    public class UserInfo
    {
        public UserInfo(string name, string university, string phone)
        {
            _fullName = name;
            _university = university;
            _phone = phone;
        }
        /// <summary>
        /// ФИО нашего пользователя
        /// </summary>
        private string _fullName;
        public string FullName { get { return _fullName; } }
        /// <summary>
        /// Название учебного заведения
        /// </summary>
        private string _university;
        public string University { get {return _university;} }
        /// <summary>
        /// Телефон пользователя
        /// </summary>
        private string _phone;
        public string Phone { get { return _phone; } }
    }
}
