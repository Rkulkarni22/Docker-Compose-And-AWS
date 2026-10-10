using LOS_Backend_Service.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LOS_Backend_Service.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HomeController : ControllerBase
    {

        private readonly LosDbContext _context;

        public HomeController(LosDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult GetUsers()
        {
           
            try
            {
                var users = _context.UserMasters.ToList();

                return Ok(users);

            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
            
        }


    }
}
