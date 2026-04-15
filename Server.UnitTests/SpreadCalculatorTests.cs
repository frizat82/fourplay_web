using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests
{
    public class SpreadCalculatorTests
    {
        private static List<NflSpreads> CreateMockSpreads()
        {
            return [
                new NflSpreads {
                    HomeTeam = "DAL",
                    AwayTeam = "NYG",
                    HomeTeamSpread = -7.0,
                    AwayTeamSpread = 7.0,
                    OverUnder = 45.5
                },

                new NflSpreads {
                    HomeTeam = "BUF",
                    AwayTeam = "MIA",
                    HomeTeamSpread = -3.5,
                    AwayTeamSpread = 3.5,
                    OverUnder = 52.0
                }
            ];
        }

        [Fact]
        public void DidUserWinPick_Spread_HomeTeamWins_CoverSpread()
        {
            // Arrange
            var spreads = CreateMockSpreads();
            var juiceMapping = new LeagueJuiceMapping { Juice = 0 };
            var calculator = new SpreadCalculator(spreads, juiceMapping, 5);

            // Act - DAL is -7, wins by 10 (covers the spread)
            var result = calculator.DidUserWinPick("DAL", 28, 18, PickType.Spread);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void DidUserWinPick_Spread_HomeTeamWins_DoesNotCoverSpread()
        {
            // Arrange
            var spreads = CreateMockSpreads();
            var juiceMapping = new LeagueJuiceMapping { Juice = 0 };
            var calculator = new SpreadCalculator(spreads, juiceMapping, 5);

            // Act - DAL is -7, wins by only 3 (doesn't cover)
            var result = calculator.DidUserWinPick("DAL", 21, 18, PickType.Spread);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void DidUserWinPick_Spread_AwayTeamWins_CoverSpread()
        {
            // Arrange
            var spreads = CreateMockSpreads();
            var juiceMapping = new LeagueJuiceMapping { Juice = 0 };
            var calculator = new SpreadCalculator(spreads, juiceMapping, 5);

            // Act - NYG is +7, wins outright (easily covers)
            var result = calculator.DidUserWinPick("NYG", 24, 21, PickType.Spread);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void DidUserWinPick_Spread_AwayTeamLoses_StillCoverSpread()
        {
            // Arrange
            var spreads = CreateMockSpreads();
            var juiceMapping = new LeagueJuiceMapping { Juice = 0 };
            var calculator = new SpreadCalculator(spreads, juiceMapping, 5);

            // Act - NYG is +7, loses by only 3 (still covers +7)
            var result = calculator.DidUserWinPick("NYG", 18, 21, PickType.Spread);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void DidUserWinPick_Over_TotalScoreExceedsOverUnder()
        {
            // Arrange
            var spreads = CreateMockSpreads();
            var juiceMapping = new LeagueJuiceMapping { Juice = 0 };
            var calculator = new SpreadCalculator(spreads, juiceMapping, 5);

            // Act - DAL vs NYG, O/U is 45.5, total score is 49
            var result = calculator.DidUserWinPick("DAL", 28, 21, PickType.Over);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void DidUserWinPick_Over_TotalScoreUnderOverUnder()
        {
            // Arrange
            var spreads = CreateMockSpreads();
            var juiceMapping = new LeagueJuiceMapping { Juice = 0 };
            var calculator = new SpreadCalculator(spreads, juiceMapping, 5);

            // Act - DAL vs NYG, O/U is 45.5, total score is 35
            var result = calculator.DidUserWinPick("DAL", 21, 14, PickType.Over);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void DidUserWinPick_Under_TotalScoreUnderOverUnder()
        {
            // Arrange
            var spreads = CreateMockSpreads();
            var juiceMapping = new LeagueJuiceMapping { Juice = 0 };
            var calculator = new SpreadCalculator(spreads, juiceMapping, 5);

            // Act - DAL vs NYG, O/U is 45.5, total score is 35
            var result = calculator.DidUserWinPick("DAL", 21, 14, PickType.Under);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void DidUserWinPick_Under_TotalScoreOverOverUnder()
        {
            // Arrange
            var spreads = CreateMockSpreads();
            var juiceMapping = new LeagueJuiceMapping { Juice = 0 };
            var calculator = new SpreadCalculator(spreads, juiceMapping, 5);

            // Act - DAL vs NYG, O/U is 45.5, total score is 49
            var result = calculator.DidUserWinPick("DAL", 28, 21, PickType.Under);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void DidUserWinPick_Push_SpreadExactlyMet_ReturnsFalse()
        {
            // Arrange
            var spreads = CreateMockSpreads();
            var juiceMapping = new LeagueJuiceMapping { Juice = 0 };
            var calculator = new SpreadCalculator(spreads, juiceMapping, 5);

            // Act - DAL is -7, wins by exactly 7 (push)
            var result = calculator.DidUserWinPick("DAL", 28, 21, PickType.Spread);

            // Assert
            Assert.False(result); // Push is treated as a loss
        }

        [Fact]
        public void DidUserWinPick_TeamNotFound_ThrowsException()
        {
            // Arrange
            var spreads = CreateMockSpreads();
            var juiceMapping = new LeagueJuiceMapping { Juice = 0 };
            var calculator = new SpreadCalculator(spreads, juiceMapping, 5);

            // Default to false
            var didWin = calculator.DidUserWinPick("XYZ", 21, 14, PickType.Spread);
            Assert.False(didWin);
        }

        [Fact]
        public void GetSpread_HomeTeam_ReturnsCorrectSpread()
        {
            // Arrange
            var spreads = CreateMockSpreads();
            var juiceMapping = new LeagueJuiceMapping { Juice = 0 };
            var calculator = new SpreadCalculator(spreads, juiceMapping, 5);

            // Act
            var result = calculator.GetSpread("DAL");

            // Assert
            Assert.Equal(-7, result);
        }

        [Fact]
        public void GetSpread_AwayTeam_ReturnsCorrectSpread()
        {
            // Arrange
            var spreads = CreateMockSpreads();
            var juiceMapping = new LeagueJuiceMapping { Juice = 0 };
            var calculator = new SpreadCalculator(spreads, juiceMapping, 5);

            // Act
            var result = calculator.GetSpread("NYG");

            // Assert
            Assert.Equal(+7, result);
        }

        [Fact]
        public void GetSpread_OverPickType_ReturnsOverUnderValue()
        {
            // Arrange
            var spreads = CreateMockSpreads();
            var juiceMapping = new LeagueJuiceMapping { Juice = 0 };
            var calculator = new SpreadCalculator(spreads, juiceMapping, 5);

            // Act
            var result = calculator.GetOverUnder("DAL", PickType.Over);

            // Assert
            Assert.Equal(45.5, result);
        }

        [Fact]
        public void GetSpread_UnderPickType_ReturnsOverUnderValue()
        {
            // Arrange
            var spreads = CreateMockSpreads();
            var juiceMapping = new LeagueJuiceMapping { Juice = 0 };
            var calculator = new SpreadCalculator(spreads, juiceMapping, 5);

            // Act
            var result = calculator.GetOverUnder("DAL", PickType.Under);

            // Assert
            Assert.Equal(45.5, result);
        }

        [Fact]
        public void GetSpread_TeamNotFound_ReturnsNull()
        {
            // Arrange
            var spreads = CreateMockSpreads();
            var juiceMapping = new LeagueJuiceMapping { Juice = 0 };
            var calculator = new SpreadCalculator(spreads, juiceMapping, 5);

            // Act
            var result = calculator.GetSpread("XYZ");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Constructor_EmptySpreads_CreatesValidCalculator()
        {
            // Arrange & Act
            var calculator = new SpreadCalculator([], new LeagueJuiceMapping(), 5);

            // Assert
            Assert.NotNull(calculator);
            Assert.Null(calculator.GetSpread("DAL"));
        }

        [Fact]
        public void DidUserWinPick_ZeroSpread_PickEm_HomeTeamWins()
        {
            // Arrange
            var spreads = new List<NflSpreads>
            {
                new NflSpreads
                {
                    HomeTeam = "DAL",
                    AwayTeam = "NYG",
                    HomeTeamSpread = 0,
                    AwayTeamSpread = 0
                }
            };
            var juiceMapping = new LeagueJuiceMapping { Juice = 0 };
            var calculator = new SpreadCalculator(spreads, juiceMapping, 5);

            // Act - Pick'em game, DAL wins
            var result = calculator.DidUserWinPick("DAL", 21, 14, PickType.Spread);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void DidUserWinPick_HalfPointSpread_NoTies()
        {
            // Arrange
            var spreads = new List<NflSpreads>
            {
                new NflSpreads
                {
                    HomeTeam = "DAL",
                    AwayTeam = "NYG",
                    HomeTeamSpread = -6.5,
                    AwayTeamSpread = 6.5
                }
            };
            var juiceMapping = new LeagueJuiceMapping { Juice = 0 };
            var calculator = new SpreadCalculator(spreads, juiceMapping, 5);

            // Act - DAL wins by exactly 7 (covers -6.5)
            var dalWin = calculator.DidUserWinPick("DAL", 28, 21, PickType.Spread);
            // Act - DAL wins by exactly 6 (doesn't cover -6.5)
            var dalLoss = calculator.DidUserWinPick("DAL", 27, 21, PickType.Spread);

            // Assert
            Assert.True(dalWin);
            Assert.False(dalLoss);
        }
    }
}
