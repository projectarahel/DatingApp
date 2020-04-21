using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DatingApp.API.Helpers;
using DatingApp.API.Models;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.API.Data
{
    public class DatingRepository : IDatingRepository
    {
        private readonly DataContext _context;

        public DatingRepository(DataContext context)
        {
            this._context = context;
        }

        public void Add<T>(T entity) where T : class
        {
            _context.Add(entity);
        }

        public void Delete<T>(T entity) where T : class
        {
            _context.Remove(entity);
        }

        public async Task<Like> GetLikeAsync(int userId, int recipientId)
        {
            return await _context.Likes.FirstOrDefaultAsync(u => u.LikerId == userId &&
                                                                u.LikeeId == recipientId);
        }

        public async Task<Photo> GetMainPhotoForUserAsync(int userId)
        {
            return await _context.Photos
                                    .Where(u => u.UserId == userId)
                                    .FirstOrDefaultAsync(p => p.IsMain);
        }

        public async Task<Photo> GetPhotoAsync(int id)
        {
            return await _context.Photos.FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<User> GetUserAsync(int id)
        {
            return await _context.Users
                                    .Include(p => p.Photos)
                                    .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<PagedList<User>> GetUsersAsync(UserParams userParams)
        {
            var users = _context.Users
                                    .Include(p => p.Photos)
                                    .OrderByDescending(u => u.LastActive)
                                    .AsQueryable();

            users = users.Where(u => u.Id != userParams.UserId &&
                                u.Gender == userParams.Gender);

            if (userParams.Likers)
            {
                var userLikers = await GetUserLikesAsync(userParams.UserId, true);
                users = users.Where(u => userLikers.Contains(u.Id));
            }

            if (userParams.Likees)
            {
                var userLikees = await GetUserLikesAsync(userParams.UserId, false);
                users = users.Where(u => userLikees.Contains(u.Id));
            }

            if (userParams.MinAge != 18 || userParams.MaxAge != 99)
            {
                var minDateOfBirth = DateTime.Today.AddYears(-userParams.MaxAge - 1);
                var maxDateOfBirth = DateTime.Today.AddYears(-userParams.MinAge);

                users = users.Where(u => u.DateOfBirth >= minDateOfBirth &&
                                u.DateOfBirth <= maxDateOfBirth);
            }

            if (!string.IsNullOrEmpty(userParams.OrderBy))
            {
                switch (userParams.OrderBy)
                {
                    case "dateCreated":
                        users = users.OrderByDescending(u => u.DateCreated);
                        break;
                    default:
                        users = users.OrderByDescending(u => u.LastActive);
                        break;
                }
            }

            return await PagedList<User>.CreateAsync(users, userParams.PageNumber, userParams.PageSize);
        }

        private async Task<IEnumerable<int>> GetUserLikesAsync(int id, bool likers)
        {
            var user = await _context.Users
                                        .Include(l => l.Likers)
                                        .Include(l => l.Likees)
                                        .FirstOrDefaultAsync(u => u.Id == id);

            if (likers)
                return user.Likers
                            .Where(l => l.LikeeId == id)
                            .Select(l => l.LikerId);
            else
                return user.Likees
                            .Where(l => l.LikerId == id)
                            .Select(l => l.LikeeId);
        }

        public async Task<bool> SaveAllAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<Message> GetMessageAsync(int id)
        {
            return await _context.Messages.FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<PagedList<Message>> GetMessagesForUserAsync(MessageParams messageParams)
        {
            var messages = _context.Messages
                                    .Include(u => u.Sender)
                                    .ThenInclude(p => p.Photos)
                                    .Include(u => u.Recipient)
                                    .ThenInclude(p => p.Photos)
                                    .AsQueryable();

            switch (messageParams.MessageContainer)
            {
                case "Inbox":
                    messages = messages.Where(u => u.RecipientId == messageParams.UserId &&
                                            !u.RecipientDeleted);
                    break;
                case "Outbox":
                    messages = messages.Where(u => u.SenderId == messageParams.UserId &&
                                            !u.SenderDeleted);
                    break;
                default:
                    messages = messages.Where(u => u.RecipientId == messageParams.UserId &&
                                            !u.RecipientDeleted &&
                                            !u.IsRead);
                    break;
            }

            messages = messages.OrderByDescending(m => m.MessageSent);

            return await PagedList<Message>.CreateAsync(messages, messageParams.PageNumber, messageParams.PageSize);
        }

        public async Task<IEnumerable<Message>> GetMessageThreadAsync(int userId, int recipientId)
        {
            var messages = await _context.Messages
                .Include(u => u.Sender).ThenInclude(p => p.Photos)
                .Include(u => u.Recipient).ThenInclude(p => p.Photos)
                .Where(m => m.RecipientId == userId &&
                    !m.RecipientDeleted &&
                    m.SenderId == recipientId  ||
                        m.RecipientId == recipientId &&
                        !m.SenderDeleted &&
                        m.SenderId == userId)
                .OrderByDescending(m => m.MessageSent)
                .ToListAsync();

            return messages;
        }
    }
}