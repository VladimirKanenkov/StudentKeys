using System;

namespace MyClient
{
    [Serializable]
    public class UserInfo
    {
        /// <summary>
        /// ФИО нашего пользователя
        /// </summary>
        public string FullName { get; set; }
        /// <summary>
        /// Название учебного заведения
        /// </summary>
        public string University { get; set; }
        /// <summary>
        /// Телефон пользователя
        /// </summary>
        public string Phone { get; set; }
    }
}
