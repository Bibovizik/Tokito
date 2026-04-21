using AutoMapper;
using Tokito.DTOs.GameDTOs;
using Tokito.DTOs.GameReviewDTOs;
using Tokito.DTOs.Genres;
using Tokito.Models;

namespace Tokito.Mappers
{
    public class GameProfile : Profile
    {
        public GameProfile()
        {
            CreateMap<Game, GameViewDTO>()
                .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Desription));
            CreateMap<Genre, GenreDTO>();
            CreateMap<GameReview, GameReviewViewDTO>();
        }
    }
}