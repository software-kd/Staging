using PagedList;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Mayflower.Models
{
    public class AdminUserViewModel
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string UserFullName { get; set; }
    }

    public class AdminEditViewModel
    {
        public string UserName { get; set; }
        public string Email { get; set; }
    }
    public class AdminRoleViewModel
    {
        public List<string> Role { get; set; }
        public string RoleId { get; set; }
    }
}