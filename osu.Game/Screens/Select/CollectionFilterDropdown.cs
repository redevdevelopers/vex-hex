// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osuTK;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// A dropdown to select the <see cref="CollectionFilter"/> to filter beatmaps using.
    /// </summary>
    public class CollectionFilterDropdown : OsuDropdown<CollectionFilter>
    {
        private readonly IBindableList<BeatmapCollection> collections = new BindableList<BeatmapCollection>();
        private readonly IBindableList<BeatmapInfo> beatmaps = new BindableList<BeatmapInfo>();
        private readonly BindableList<CollectionFilter> filters = new BindableList<CollectionFilter>();

        public CollectionFilterDropdown()
        {
            ItemSource = filters;
        }

        [BackgroundDependencyLoader]
        private void load(BeatmapCollectionManager collectionManager)
        {
            collections.BindTo(collectionManager.Collections);
            collections.CollectionChanged += (_, __) => collectionsChanged();
            collectionsChanged();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Current.BindValueChanged(filterChanged, true);
        }

        /// <summary>
        /// Occurs when a collection has been added or removed.
        /// </summary>
        private void collectionsChanged()
        {
            var selectedItem = SelectedItem?.Value?.Collection;

            filters.Clear();
            filters.Add(new CollectionFilter(null));
            filters.AddRange(collections.Select(c => new CollectionFilter(c)));

            Current.Value = filters.SingleOrDefault(f => f.Collection == selectedItem) ?? filters[0];
        }

        /// <summary>
        /// Occurs when the <see cref="CollectionFilter"/> selection has changed.
        /// </summary>
        private void filterChanged(ValueChangedEvent<CollectionFilter> filter)
        {
            // Binding the beatmaps will trigger a collection change event, which results in an infinite-loop. This is rebound later, when it's safe to do so.
            beatmaps.CollectionChanged -= filterBeatmapsChanged;

            if (filter.OldValue?.Collection != null)
                beatmaps.UnbindFrom(filter.OldValue.Collection.Beatmaps);

            if (filter.NewValue?.Collection != null)
                beatmaps.BindTo(filter.NewValue.Collection.Beatmaps);

            beatmaps.CollectionChanged += filterBeatmapsChanged;
        }

        /// <summary>
        /// Occurs when the beatmaps contained by a <see cref="BeatmapCollection"/> have changed.
        /// </summary>
        private void filterBeatmapsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // The filtered beatmaps have changed, without the filter having changed itself. So a change in filter must be notified.
            // Note that this does NOT propagate to bound bindables, so the FilterControl must bind directly to the value change event of this bindable.
            Current.TriggerChange();
        }

        protected override string GenerateItemText(CollectionFilter item) => item.Collection?.Name.Value ?? "All beatmaps";

        protected override DropdownHeader CreateHeader() => new CollectionDropdownHeader
        {
            SelectedItem = { BindTarget = Current }
        };

        protected override DropdownMenu CreateMenu() => new CollectionDropdownMenu();

        public class CollectionDropdownHeader : OsuDropdownHeader
        {
            public readonly Bindable<CollectionFilter> SelectedItem = new Bindable<CollectionFilter>();
            private readonly Bindable<string> collectionName = new Bindable<string>();

            protected override string Label
            {
                get => base.Label;
                set { } // See updateText().
            }

            public CollectionDropdownHeader()
            {
                Height = 25;
                Icon.Size = new Vector2(16);
                Foreground.Padding = new MarginPadding { Top = 4, Bottom = 4, Left = 8, Right = 4 };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                SelectedItem.BindValueChanged(_ => updateBindable(), true);
            }

            private void updateBindable()
            {
                collectionName.UnbindAll();

                if (SelectedItem.Value != null)
                    collectionName.BindTo(SelectedItem.Value.CollectionName);

                collectionName.BindValueChanged(_ => updateText(), true);
            }

            // Dropdowns don't bind to value changes, so the real name is copied directly from the selected item here.
            private void updateText() => base.Label = collectionName.Value;
        }

        private class CollectionDropdownMenu : OsuDropdownMenu
        {
            public CollectionDropdownMenu()
            {
                MaxHeight = 200;
            }

            protected override DrawableDropdownMenuItem CreateDrawableDropdownMenuItem(MenuItem item) => new CollectionDropdownMenuItem(item);
        }

        private class CollectionDropdownMenuItem : OsuDropdownMenu.DrawableOsuDropdownMenuItem
        {
            [NotNull]
            protected new CollectionFilter Item => ((DropdownMenuItem<CollectionFilter>)base.Item).Value;

            [Resolved]
            private OsuColour colours { get; set; }

            [Resolved]
            private IBindable<WorkingBeatmap> beatmap { get; set; }

            [CanBeNull]
            private readonly BindableList<BeatmapInfo> collectionBeatmaps;

            [NotNull]
            private readonly Bindable<string> collectionName;

            private IconButton addOrRemoveButton;
            private Content content;
            private bool beatmapInCollection;

            public CollectionDropdownMenuItem(MenuItem item)
                : base(item)
            {
                collectionBeatmaps = Item.Collection?.Beatmaps.GetBoundCopy();
                collectionName = Item.CollectionName.GetBoundCopy();
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                AddInternal(addOrRemoveButton = new IconButton
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -OsuScrollContainer.SCROLL_BAR_HEIGHT,
                    Scale = new Vector2(0.7f),
                    AlwaysPresent = true,
                    Alpha = collectionBeatmaps == null ? 0 : 1,
                    Action = addOrRemove,
                });
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                if (collectionBeatmaps != null)
                {
                    collectionBeatmaps.CollectionChanged += (_, __) => collectionChanged();
                    beatmap.BindValueChanged(_ => collectionChanged(), true);
                }

                // Although the DrawableMenuItem binds to value changes of the item's text, the item is an internal implementation detail of Dropdown that has no knowledge
                // of the underlying CollectionFilter value and its accompanying name, so the real name has to be copied here. Without this, the collection name wouldn't update when changed.
                collectionName.BindValueChanged(name => content.Text = name.NewValue, true);
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateButtonVisibility();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateButtonVisibility();
                base.OnHoverLost(e);
            }

            private void collectionChanged()
            {
                Debug.Assert(collectionBeatmaps != null);

                beatmapInCollection = collectionBeatmaps.Contains(beatmap.Value.BeatmapInfo);

                addOrRemoveButton.Enabled.Value = !beatmap.IsDefault;
                addOrRemoveButton.Icon = beatmapInCollection ? FontAwesome.Solid.MinusSquare : FontAwesome.Solid.PlusSquare;
                addOrRemoveButton.TooltipText = beatmapInCollection ? "Remove selected beatmap" : "Add selected beatmap";

                updateButtonVisibility();
            }

            private void updateButtonVisibility() => addOrRemoveButton.Alpha = IsHovered || beatmapInCollection ? 1 : 0;

            private void addOrRemove()
            {
                Debug.Assert(collectionBeatmaps != null);

                if (!collectionBeatmaps.Remove(beatmap.Value.BeatmapInfo))
                    collectionBeatmaps.Add(beatmap.Value.BeatmapInfo);
            }

            protected override Drawable CreateContent() => content = (Content)base.CreateContent();
        }
    }
}
