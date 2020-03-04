// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osuTK;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.UserInterface;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics.Containers;
using osu.Game.Users.Drawables;
using JetBrains.Annotations;
using osu.Framework.Input.Events;

namespace osu.Game.Users
{
    public abstract class UserPanel : OsuClickableContainer, IHasContextMenu
    {
        public readonly User User;

        public readonly Bindable<UserStatus> Status = new Bindable<UserStatus>();

        public readonly IBindable<UserActivity> Activity = new Bindable<UserActivity>();

        public new Action Action;

        protected Action ViewProfile;

        protected DelayedLoadUnloadWrapper Background;

        private SpriteIcon statusIcon;
        private OsuSpriteText statusMessage;
        private TextFlowContainer lastVisitMessage;

        protected UserPanel(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            User = user;
        }

        [Resolved(canBeNull: true)]
        private UserProfileOverlay profileOverlay { get; set; }

        [Resolved]
        private OsuColour colours { get; set; }

        [BackgroundDependencyLoader]
        private void load()
        {
            Action = () => profileOverlay?.ShowUser(User);

            Masking = true;
            BorderColour = colours.GreyVioletLighter;

            AddRange(new[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colours.Gray1
                },
                Background = new DelayedLoadUnloadWrapper(() => new UserCoverBackground
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    User = User,
                }, 300, 5000)
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    RelativeSizeAxes = Axes.Both,
                },
                CreateLayout()
            });

            Status.ValueChanged += status => displayStatus(status.NewValue, Activity.Value);
            Activity.ValueChanged += activity => displayStatus(Status.Value, activity.NewValue);

            base.Action = ViewProfile = () =>
            {
                Action?.Invoke();
                profileOverlay?.ShowUser(User);
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Status.TriggerChange();
        }

        protected override bool OnHover(HoverEvent e)
        {
            BorderThickness = 2;
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            BorderThickness = 0;
            base.OnHoverLost(e);
        }

        [NotNull]
        protected abstract Drawable CreateLayout();

        protected UpdateableAvatar CreateAvatar() => new UpdateableAvatar
        {
            User = User,
            OpenOnClick = { Value = false }
        };

        protected UpdateableFlag CreateFlag() => new UpdateableFlag(User.Country)
        {
            Size = new Vector2(39, 26)
        };

        protected OsuSpriteText CreateUsername() => new OsuSpriteText
        {
            Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold, italics: true),
            Text = User.Username,
        };

        protected SpriteIcon CreateStatusIcon() => statusIcon = new SpriteIcon
        {
            Icon = FontAwesome.Regular.Circle,
            Size = new Vector2(25)
        };

        protected FillFlowContainer CreateStatusMessage(bool rightAlignedChildren)
        {
            var statusContainer = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical
            };

            var alignment = rightAlignedChildren ? Anchor.x2 : Anchor.x0;

            statusContainer.Add(lastVisitMessage = new TextFlowContainer(t => t.Font = OsuFont.GetFont(size: 15)).With(text =>
            {
                text.Anchor = Anchor.y1 | alignment;
                text.Origin = Anchor.y1 | alignment;
                text.AutoSizeAxes = Axes.Both;
                text.Alpha = 0;

                if (User.LastVisit.HasValue)
                {
                    text.AddText(@"Last seen ");
                    text.AddText(new DrawableDate(User.LastVisit.Value, italic: false));
                }
            }));

            statusContainer.Add(statusMessage = new OsuSpriteText
            {
                Anchor = Anchor.y1 | alignment,
                Origin = Anchor.y1 | alignment,
                Font = OsuFont.GetFont(size: 17)
            });

            return statusContainer;
        }

        private void displayStatus(UserStatus status, UserActivity activity = null)
        {
            if (status != null)
            {
                if (activity != null && !(status is UserStatusOffline))
                {
                    statusMessage.Text = activity.Status;
                    statusIcon.FadeColour(activity.GetAppropriateColour(colours), 500, Easing.OutQuint);
                }
                else
                {
                    lastVisitMessage.FadeTo(status is UserStatusOffline && User.LastVisit.HasValue ? 1 : 0);
                    statusMessage.Text = status.Message;
                    statusIcon.FadeColour(status.GetAppropriateColour(colours), 500, Easing.OutQuint);
                }

                return;
            }

            // Set local status according to web if it's null
            if (User.IsOnline)
            {
                Status.Value = new UserStatusOnline();
                return;
            }

            Status.Value = new UserStatusOffline();
        }

        public MenuItem[] ContextMenuItems => new MenuItem[]
        {
            new OsuMenuItem("View Profile", MenuItemType.Highlighted, ViewProfile),
        };
    }
}
