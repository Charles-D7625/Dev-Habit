using System.Dynamic;
using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Common;
using DevHabit.Api.DTOs.Habits;
using DevHabit.Api.DTOs.HabitTags;
using DevHabit.Api.Entities;
using DevHabit.Api.Services;
using DevHabit.Api.Services.Sorting;
using FluentValidation;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevHabit.Api.Controllers;

[ApiController]
[Route("habits")]
public sealed class HabitController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetHabits(
        [FromQuery] HabitsQueryParameters queryParameters,
        SortMappingProvider sortMappingProvider,
        DataShapingService dataShapingService)
    {
        if (!sortMappingProvider.ValidateMapping<HabitDto, Habit>(queryParameters.Sort))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided query parameters are invalid: '{queryParameters.Sort}'.");
        }

        if (!dataShapingService.Validate<HabitDto>(queryParameters.Fields))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided data shaping fields are invalid: '{queryParameters.Fields}'.");
        }
        
        queryParameters.Search ??= queryParameters.Search?.Trim().ToLower();

        SortMapping[] sortMappings = sortMappingProvider.GetMappings<HabitDto, Habit>();

        IQueryable<HabitDto> habitsQuery = dbContext
            .Habits
#pragma warning disable CA1862
            .Where(h => queryParameters.Search == null ||
                        h.Name.ToLower().Contains(queryParameters.Search.ToLower()) ||
                        h.Description != null && h.Description.ToLower().Contains(queryParameters.Search.ToLower()))
            .Where(h => queryParameters.Type == null || h.Type == queryParameters.Type)
            .Where(h => queryParameters.Status == null || h.Status == queryParameters.Status)
            .ApplySort(queryParameters.Sort, sortMappings)
            .Select(HabitQueries.ProjectToDto());
#pragma warning restore CA1862

        int totalCount = await habitsQuery.CountAsync();
        
        List<HabitDto> habits = await habitsQuery
            .Skip((queryParameters.Page - 1) * queryParameters.PageSize)
            .Take(queryParameters.PageSize)
            .ToListAsync();

        var paginationResult = new PaginationResult<ExpandoObject>
        {
            Items = dataShapingService.ShapeDataList(habits, queryParameters.Fields),
            Page = queryParameters.Page,
            PageSize = queryParameters.PageSize,
            TotalCount = totalCount
        };
        
        return Ok(paginationResult);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetHabit(
        string id,
        string? fields,
        DataShapingService dataShapingService)
    {
        
        if (!dataShapingService.Validate<HabitWithTagsDto>(fields))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided data shaping fields are invalid: '{fields}'.");
        }
        
        HabitWithTagsDto? habit = await dbContext
            .Habits
            .Where(h => h.Id == id)
            .Select(HabitQueries.ProjectToHabitWithTagsToDto())
            .FirstOrDefaultAsync();

        if (habit is null)
        {
            return NotFound();
        }
        
        ExpandoObject shapedHabitDto = dataShapingService.ShapeData(habit, fields);
        
        return Ok(shapedHabitDto);
    }

    [HttpPost]
    public async Task<ActionResult<HabitDto>> CreateHabit(
        CreateHabitDto createHabitDto,
        IValidator<CreateHabitDto> validator)
    {
        await validator.ValidateAndThrowAsync(createHabitDto);
        
        Habit habit = createHabitDto.ToEntity();
        
        dbContext.Habits.Add(habit);
        
        await dbContext.SaveChangesAsync();
        
        HabitDto habitDto = habit.toDto();
        
        return CreatedAtAction(nameof(GetHabit), new { id = habit.Id }, habitDto);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateHabit(string id, UpdateHabitDto updateHabitDto)
    {
        Habit? habit = await dbContext.Habits.FirstOrDefaultAsync(h => h.Id == id);

        if (habit == null)
        {
            return NotFound();
        }
        
        habit.UpdateFromDto(updateHabitDto);
        
        await dbContext.SaveChangesAsync();
        
        return NoContent();
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult> PatchHabit(string id, JsonPatchDocument<HabitDto> patchDoc)
    {
        Habit? habit = await dbContext.Habits.FirstOrDefaultAsync(h => h.Id == id);

        if (habit == null)
        {
            return NotFound();
        }
        
        HabitDto updatedHabitDto = habit.toDto();
        
        patchDoc.ApplyTo(updatedHabitDto, ModelState);

        if (!TryValidateModel(updatedHabitDto))
        {
            return ValidationProblem(ModelState);
        }
        
        habit.Name = updatedHabitDto.Name;
        habit.Description = updatedHabitDto.Description;
        habit.UpdatedAtUtc = DateTime.UtcNow;
        
        await dbContext.SaveChangesAsync();
        
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteHabit(string id)
    {
        Habit? habit = await dbContext.Habits.FirstOrDefaultAsync(h => h.Id == id);

        if (habit is null)
        {
            return NotFound();
        }
        
        dbContext.Habits.Remove(habit);
        
        await dbContext.SaveChangesAsync();
        
        return NoContent();
    }
}
