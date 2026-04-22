using AutoMapper;
using Tokito.DTOs.GameReviewDTOs;
using Tokito.DTOs.Genres;
using Tokito.Models;

namespace Tokito.Mappers
{
    public class GameProfile : Profile
    {
        public GameProfile()
        {
            CreateMap<Genre, GenreDTO>();
            CreateMap<GameReview, GameReviewViewDTO>();
        }
    }
}
