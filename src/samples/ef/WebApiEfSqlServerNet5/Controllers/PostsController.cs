using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApiEfSqlServerNet5.Data;
using WebApiEfSqlServerNet5.Models;

namespace WebApiEfSqlServerNet5.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PostsController : ControllerBase
    {
        private readonly BlogDbContext _db;

        public PostsController(BlogDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Post>>> GetAll()
        {
            var posts = await _db.Posts
                .Include(p => p.Comments)
                .AsNoTracking()
                .ToListAsync();

            return Ok(posts);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<Post>> GetById(Guid id)
        {
            var post = await _db.Posts
                .Include(p => p.Comments)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null)
                return NotFound();

            return Ok(post);
        }

        [HttpPost]
        public async Task<ActionResult<Post>> Create([FromBody] PostRequest request)
        {
            var post = new Post
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                Body = request.Body,
                Author = request.Author,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Posts.Add(post);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = post.Id }, post);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] PostRequest request)
        {
            var post = await _db.Posts.FindAsync(id);

            if (post == null)
                return NotFound();

            post.Title = request.Title;
            post.Body = request.Body;
            post.Author = request.Author;
            post.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var post = await _db.Posts.FindAsync(id);

            if (post == null)
                return NotFound();

            _db.Posts.Remove(post);
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }

    public class PostRequest
    {
        public string Title { get; set; }
        public string Body { get; set; }
        public string Author { get; set; }
    }
}
