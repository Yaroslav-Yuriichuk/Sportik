using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Windows.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Sportik.Desktop.Core.Events;
using Sportik.Desktop.Core.Models;
using Sportik.Desktop.Core.Services.Interfaces;
using Sportik.Desktop.UI.Helpers;
using Sportik.Desktop.UI.Models;
using Sportik.Desktop.UI.Services.Interfaces;
using Sportik.Desktop.UI.Views.Internal;

namespace Sportik.Desktop.UI.ViewModels.Navigation
{
    internal sealed class InternalNavigationViewModel : ViewModel, IDisposable
    {
        private ObservableCollection<NavigationOption> _options;

        public ObservableCollection<NavigationOption> MenuItemOptions
        {
            get => _options;
            set
            {
                if (SetField(ref _options, value))
                {
                    SelectedMenuItem = _options?[0];
                    NavigationService.Navigate(_options?[0]?.PageType, NavigationScope.Internal);
                }
            }
        }

        private object _selectedMenuItem;

        public object SelectedMenuItem
        {
            get => _selectedMenuItem;
            set => SetField(ref _selectedMenuItem, value);
        }

        public ICommand SelectionChangedCommand { get; }

        private INavigationService NavigationService => App.ServiceProvider.GetService<INavigationService>();
        private IEventsService EventsService => App.ServiceProvider.GetService<IEventsService>();

        public InternalNavigationViewModel()
        {
            SelectionChangedCommand = new LazyRelayCommand<NavigationViewSelectionChangedEventArgs>(HandleSelection);

            MenuItemOptions = new ObservableCollection<NavigationOption>
            {
                new NavigationOption { Name = "Exercises", Icon = Symbol.AllApps, PageType = typeof(ExercisesPage), },
                new NavigationOption { Name = "Create Exercise", Icon = Symbol.Add, PageType = typeof(CreateExercisePage), },
                new NavigationOption { Name = "Exercise Statistics", Icon = Symbol.ViewAll, PageType = typeof(ExerciseStatisticsPage), },
                new NavigationOption { Name = "Dashboard", Icon = Symbol.FourBars, PageType = typeof(DashboardPage), },
                new NavigationOption { Name = "Extra Sets", Icon = Symbol.Favorite, PageType = typeof(ExtraSetsPage), },
                new NavigationOption { Name = "Exercise Settings", Icon = Symbol.Edit, PageType = typeof(ExerciseSettingsPage), },
                new NavigationOption { Name = "Training", Icon = Symbol.Shuffle, PageType = typeof(TrainingPage), },
                new NavigationOption { Name = "Account", Icon = Symbol.Contact, PageType = typeof(AccountPage), },
            };

            EventsService.AddListener<ReminderNotificationAcceptedEventArgs>(EventsService_Event);
            EventsService.AddListener<ExerciseCreatedEventArgs>(EventsService_Event);
        }

        public void Dispose()
        {
            EventsService.RemoveListener<ReminderNotificationAcceptedEventArgs>(EventsService_Event);
            EventsService.RemoveListener<ExerciseCreatedEventArgs>(EventsService_Event);
        }

        private void HandleSelection(NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                NavigationService.Navigate(typeof(ExerciseSettingsPage), NavigationScope.Internal);
                return;
            }

            if (args.SelectedItem is NavigationOption menuItem)
            {
                NavigationService.Navigate(menuItem.PageType, NavigationScope.Internal);
            }
        }

        private void EventsService_Event(ReminderNotificationAcceptedEventArgs args)
        {
            NavigationOption option = MenuItemOptions.FirstOrDefault(o => o.PageType == typeof(ExercisesPage));

            if (option != null && SelectedMenuItem != option)
            {
                _ = UIThreadHelper.RunOnUIThreadAsync(() =>
                {
                    SelectedMenuItem = option;
                });
            }
        }

        private void EventsService_Event(ExerciseCreatedEventArgs args)
        {
            if (args.CreationSource != CreationSource.User)
            {
                return;
            }

            NavigationOption option = MenuItemOptions.FirstOrDefault(o => o.PageType == typeof(ExercisesPage));

            if (option != null && SelectedMenuItem != option)
            {
                _ = UIThreadHelper.RunOnUIThreadAsync(() =>
                {
                    SelectedMenuItem = option;
                });
            }
        }
    }
}
