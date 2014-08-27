#region Copyright
// ****************************************************************************
// <copyright file="AttachedMembersModuleView.cs">
// Copyright � Vyacheslav Volkov 2012-2014
// </copyright>
// ****************************************************************************
// <author>Vyacheslav Volkov</author>
// <email>vvs0205@outlook.com</email>
// <project>MugenMvvmToolkit</project>
// <web>https://github.com/MugenMvvmToolkit/MugenMvvmToolkit</web>
// <license>
// See license.txt in this solution or http://opensource.org/licenses/MS-PL
// </license>
// ****************************************************************************
#endregion
using System;
using Android.Views;
using MugenMvvmToolkit.Binding;
using MugenMvvmToolkit.Binding.Interfaces;
using MugenMvvmToolkit.Binding.Interfaces.Models;
using MugenMvvmToolkit.Binding.Models;
using MugenMvvmToolkit.Binding.Models.EventArg;
using MugenMvvmToolkit.Models;

// ReSharper disable once CheckNamespace
namespace MugenMvvmToolkit.Infrastructure
{
    public partial class AttachedMembersModule
    {
        #region Nested types

        private abstract class LayoutObserver : DisposableObject
        {
            #region Fields

            private WeakReference _viewReference;

            #endregion

            #region Constructors

            protected LayoutObserver(View view)
            {
                _viewReference = ServiceProvider.WeakReferenceFactory(view, true);
#if API17
                view.LayoutChange += OnGlobalLayoutChanged;
#else
                if (view.ViewTreeObserver.IsAlive)
                    view.ViewTreeObserver.GlobalLayout += OnGlobalLayoutChanged;
#endif
            }

            #endregion

            #region Methods

            protected View GetView()
            {
                var viewReference = _viewReference;
                if (viewReference == null)
                    return null;
                return (View)viewReference.Target;
            }

            private void OnGlobalLayoutChanged(object sender, EventArgs eventArgs)
            {
                if (IsDisposed)
                    return;
                var view = GetView();
                if (view == null)
                    Dispose();
                else
                    OnGlobalLayoutChangedInternal(view, sender, eventArgs);
            }

            protected abstract void OnGlobalLayoutChangedInternal(View view, object sender, EventArgs eventArgs);

            #endregion

            #region Overrides of DisposableObjectBase

            protected override void OnDispose(bool disposing)
            {
                base.OnDispose(disposing);
                if (disposing)
                {
                    try
                    {
                        var view = (View)_viewReference.Target;
#if API17
                        view.LayoutChange -= OnGlobalLayoutChanged;
#else
                        if (view != null && view.ViewTreeObserver.IsAlive)
                            view.ViewTreeObserver.GlobalLayout -= OnGlobalLayoutChanged;
#endif
                    }
                    catch (Exception e)
                    {
                        Tracer.Warn(e.Flatten());
                    }
                    finally
                    {
                        _viewReference = null;
                    }
                }
            }

            #endregion
        }

        private sealed class VisiblityObserver : LayoutObserver
        {
            #region Fields

            private readonly object _listenerRef;
            private ViewStates _oldValue;

            #endregion

            #region Constructors

            public VisiblityObserver(View view, IEventListener handler)
                : base(view)
            {
                _listenerRef = handler.ToWeakItem();
                _oldValue = view.Visibility;
            }

            #endregion

            #region Overrides of LayoutObserver

            protected override void OnGlobalLayoutChangedInternal(View view, object sender, EventArgs eventArgs)
            {
                ViewStates visibility = view.Visibility;
                if (_oldValue == visibility)
                    return;
                _oldValue = visibility;
                var listener = BindingExtensions.GetEventListenerFromWeakItem(_listenerRef);
                if (listener == null)
                    Dispose();
                else
                    listener.Handle(view, eventArgs);
            }

            #endregion
        }

        #endregion

        #region Fields

        internal static readonly IAttachedBindingMemberInfo<View, bool> DisableValidationMember;

        #endregion

        #region Methods

        private static void RegisterViewMembers(IBindingMemberProvider memberProvider)
        {
            //View
            memberProvider.Register(DisableValidationMember);
            memberProvider.Register(AttachedBindingMember.CreateAutoProperty<View, int>(AttachedMemberNames.MenuTemplate));

#if !API8
            memberProvider.Register(AttachedBindingMember.CreateAutoProperty<View, int>(AttachedMemberNames.PopupMenuTemplate));
            memberProvider.Register(AttachedBindingMember.CreateAutoProperty<View, string>(AttachedMemberNames.PopupMenuEvent, PopupMenuEventChanged));
            memberProvider.Register(AttachedBindingMember.CreateAutoProperty<View, string>(AttachedMemberNames.PlacementTargetPath));
#endif
            memberProvider.Register(AttachedBindingMember.CreateMember<View, object>(AttachedMemberConstants.Parent,
                    GetViewParentValue, null, ObserveViewParent));
            memberProvider.Register(AttachedBindingMember.CreateMember<View, object>(AttachedMemberConstants.FindByNameMethod,
                    ViewFindByNameMember, null));
            memberProvider.Register(AttachedBindingMember.CreateMember<View, bool>(AttachedMemberConstants.Focused,
                    (info, view, arg3) => view.IsFocused, null, memberChangeEventName: "FocusChange"));
            memberProvider.Register(AttachedBindingMember.CreateMember<View, bool>(AttachedMemberConstants.Enabled,
                    (info, view, arg3) => view.Enabled, (info, view, arg3) => view.Enabled = (bool)arg3[0]));
            memberProvider.Register(AttachedBindingMember.CreateMember<View, ViewStates>("Visibility",
                    (info, view, arg3) => view.Visibility, (info, view, arg3) => view.Visibility = (ViewStates)arg3[0],
                    ObserveViewVisibility));
            memberProvider.Register(AttachedBindingMember.CreateMember<View, bool>("Visible",
                    (info, view, arg3) => view.Visibility == ViewStates.Visible,
                    (info, view, arg3) => view.Visibility = ((bool)arg3[0]) ? ViewStates.Visible : ViewStates.Gone,
                    ObserveViewVisibility));
            memberProvider.Register(AttachedBindingMember.CreateMember<View, bool>("Hidden",
                    (info, view, arg3) => view.Visibility != ViewStates.Visible,
                    (info, view, arg3) => view.Visibility = ((bool)arg3[0]) ? ViewStates.Gone : ViewStates.Visible,
                    ObserveViewVisibility));
        }

        private static IDisposable ObserveViewVisibility(IBindingMemberInfo bindingMemberInfo, View view, IEventListener arg3)
        {
            return new VisiblityObserver(view, arg3);
        }

        private static IDisposable ObserveViewParent(IBindingMemberInfo bindingMemberInfo, View view, IEventListener arg3)
        {
            return ParentObserver.GetOrAdd(view).AddWithUnsubscriber(arg3);
        }

        private static object GetViewParentValue(IBindingMemberInfo arg1, View view, object[] arg3)
        {
            if (view.Id == Android.Resource.Id.Content)
                return view.Context.GetActivity();
            return ParentObserver.GetOrAdd(view).Parent;
        }

        private static object ViewFindByNameMember(IBindingMemberInfo bindingMemberInfo, View target, object[] arg3)
        {
            var tag = arg3[0].ToStringSafe();
            return target.RootView.FindViewWithTag(tag);
        }

#if !API8
        private static void PopupMenuEventChanged(View view, AttachedMemberChangedEventArgs<string> args)
        {
            if (string.IsNullOrEmpty(args.NewValue))
                return;
            IBindingMemberInfo member = BindingServiceProvider
                .MemberProvider
                .GetBindingMember(view.GetType(), args.NewValue, false, true);
            var presenter = ServiceProvider.AttachedValueProvider.GetOrAdd(view, "!@popup", (view1, o) => new PopupMenuPresenter(view1), null);
            var unsubscriber = member.SetValue(view, new object[] { presenter }) as IDisposable;
            presenter.Update(unsubscriber);
        }
#endif
        #endregion
    }
}