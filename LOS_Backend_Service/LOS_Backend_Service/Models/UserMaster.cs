using System;
using System.Collections.Generic;

namespace LOS_Backend_Service.Models;

public partial class UserMaster
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public int? Age { get; set; }
}
