using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApiEfSqlServerNet5.Data;
using WebApiEfSqlServerNet5.Models;

namespace WebApiEfSqlServerNet5.Controllers
{
    [ApiController]
    [Route("api/posts/{postId:guid}/[controller]")]
    public class CommentsController : ControllerBase
    {
        private readonly BlogDbContext _db;

        public CommentsController(BlogDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Comment>>> GetAll(Guid postId)
        {
            var postExists = await _db.Posts.AnyAsync(p => p.Id == postId);
            if (!postExists)
                return NotFound($"Post {postId} not found.");

            var comments = await _db.Comments
                .AsNoTracking()
                .Where(c => c.PostId == postId)
                .ToListAsync();

            return Ok(comments);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<Comment>> GetById(Guid postId, Guid id)
        {
            var comment = await _db.Comments
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id && c.PostId == postId);

            if (comment == null)
                return NotFound();

            return Ok(comment);
        }

        [HttpPost]
        public async Task<ActionResult<Comment>> Create(Guid postId, [FromBody] CommentRequest request)
        {
            var postExists = await _db.Posts.AnyAsync(p => p.Id == postId);
            if (!postExists)
                return NotFound($"Post {postId} not found.");

            var comment = new Comment
            {
                Id = Guid.NewGuid(),
                PostId = postId,
                Author = request.Author,
                Content = request.Content,
                CreatedAt = DateTime.UtcNow
            };

            _db.Comments.Add(comment);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { postId, id = comment.Id }, comment);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid postId, Guid id)
        {
            var comment = await _db.Comments
                .FirstOrDefaultAsync(c => c.Id == id && c.PostId == postId);

            if (comment == null)
                return NotFound();

            _db.Comments.Remove(comment);
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }

    public class CommentRequest
    {
        public string Author { get; set; }
        public string Content { get; set; }
    }
}
