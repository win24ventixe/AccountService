using Presentation.Data.Entities;

namespace Presentation.Data.Repositories;
public interface IUserRepository : IBaseRepository<UserEntity>
{
}
public class UserRepository(DataContext context) : BaseRepository<UserEntity>(context), IUserRepository
{
}