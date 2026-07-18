using AutoSysJilBlazor.Data;

namespace AutoSysJilBlazor.Tests.Data;

[TestFixture]
public class WeatherForecastServiceTests
{
    [Test]
    public async Task GetForecastAsync_ReturnsFiveDaysWithExpectedDateOffsets()
    {
        var service = new WeatherForecastService();
        var startDate = new DateOnly(2026, 1, 1);

        var forecasts = await service.GetForecastAsync(startDate);

        Assert.That(forecasts, Has.Length.EqualTo(5));
        Assert.That(forecasts.Select(x => x.Date), Is.EqualTo(new[]
        {
            startDate.AddDays(1),
            startDate.AddDays(2),
            startDate.AddDays(3),
            startDate.AddDays(4),
            startDate.AddDays(5)
        }));
    }

    [Test]
    public async Task GetForecastAsync_ProducesValuesWithinExpectedRanges()
    {
        var service = new WeatherForecastService();
        var startDate = new DateOnly(2026, 1, 1);
        var validSummaries = new HashSet<string>
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        var forecasts = await service.GetForecastAsync(startDate);

        Assert.Multiple(() =>
        {
            Assert.That(forecasts.All(x => x.TemperatureC >= -20 && x.TemperatureC < 55), Is.True);
            Assert.That(forecasts.All(x => x.Summary is not null && validSummaries.Contains(x.Summary)), Is.True);
        });
    }

    [Test]
    public void WeatherForecast_TemperatureF_ComputesFromTemperatureC()
    {
        var model = new WeatherForecast { TemperatureC = 0 };

        Assert.That(model.TemperatureF, Is.EqualTo(32));
    }
}
