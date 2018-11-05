﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Reactive;
using System.Threading.Tasks;
using GitHub.Extensions;
using GitHub.Factories;
using GitHub.Models;
using GitHub.Primitives;
using GitHub.Services;
using ReactiveUI;

namespace GitHub.ViewModels.Documents
{
    /// <summary>
    /// View model for displaying a pull request in a document window.
    /// </summary>
    [Export(typeof(IPullRequestPageViewModel))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class PullRequestPageViewModel : PullRequestViewModelBase, IPullRequestPageViewModel, IIssueishCommentThreadViewModel
    {
        readonly IViewViewModelFactory factory;
        readonly IPullRequestService service;
        readonly IPullRequestSessionManager sessionManager;
        readonly ITeamExplorerServices teServices;
        ActorModel currentUserModel;
        ReactiveList<IViewModel> timeline;

        /// <summary>
        /// Initializes a new instance of the <see cref="PullRequestPageViewModel"/> class.
        /// </summary>
        /// <param name="factory">The view model factory.</param>
        [ImportingConstructor]
        public PullRequestPageViewModel(
            IViewViewModelFactory factory,
            IPullRequestService service,
            IPullRequestSessionManager sessionManager,
            ITeamExplorerServices teServices)
        {
            Guard.ArgumentNotNull(factory, nameof(factory));
            Guard.ArgumentNotNull(service, nameof(service));
            Guard.ArgumentNotNull(sessionManager, nameof(sessionManager));
            Guard.ArgumentNotNull(teServices, nameof(teServices));

            this.factory = factory;
            this.service = service;
            this.sessionManager = sessionManager;
            this.teServices = teServices;

            ShowCommit = ReactiveCommand.CreateFromTask<string>(DoShowCommit);
        }

        /// <inheritdoc/>
        public IActorViewModel CurrentUser { get; private set; }

        /// <inheritdoc/>
        public IReadOnlyList<IViewModel> Timeline => timeline;

        /// <inheritdoc/>
        public ReactiveCommand<string, Unit> ShowCommit { get; }

        /// <inheritdoc/>
        public async Task InitializeAsync(
            IRemoteRepositoryModel repository,
            ILocalRepositoryModel localRepository,
            ActorModel currentUser,
            PullRequestDetailModel model)
        {
            await base.InitializeAsync(repository, localRepository, model).ConfigureAwait(true);

            currentUserModel = currentUser;
            CurrentUser = new ActorViewModel(currentUser);
            timeline = new ReactiveList<IViewModel>();

            var commits = new List<CommitSummaryViewModel>();

            foreach (var i in model.Timeline)
            {
                if (!(i is CommitModel) && commits.Count > 0)
                {
                    timeline.Add(new CommitSummariesViewModel(commits));
                    commits.Clear();
                }

                switch (i)
                {
                    case CommitModel commit:
                        commits.Add(new CommitSummaryViewModel(commit));
                        break;
                    case CommentModel comment:
                        await AddComment(comment).ConfigureAwait(true);
                        break;
                }
            }

            if (commits.Count > 0)
            {
                timeline.Add(new CommitSummariesViewModel(commits));
            }

            var placeholder = factory.CreateViewModel<IIssueishCommentViewModel>();
            await placeholder.InitializeAsync(
                this,
                currentUser,
                null,
                Resources.ClosePullRequest).ConfigureAwait(true);
            timeline.Add(placeholder);
        }

        /// <inheritdoc/>
        public async Task PostComment(string body)
        {
            var address = HostAddress.Create(Repository.CloneUrl);
            var comment = await service.PostComment(address, Id, body).ConfigureAwait(true);
            await AddComment(comment).ConfigureAwait(true);
            ClearPlaceholder();
        }

        Task ICommentThreadViewModel.DeleteComment(int pullRequestId, int commentId)
        {
            throw new NotImplementedException();
        }

        Task ICommentThreadViewModel.EditComment(string id, string body)
        {
            throw new NotImplementedException();
        }

        Task IIssueishCommentThreadViewModel.CloseIssueish(string body)
        {
            throw new NotImplementedException();
        }

        async Task AddComment(CommentModel comment)
        {
            var vm = factory.CreateViewModel<IIssueishCommentViewModel>();
            await vm.InitializeAsync(this, currentUserModel, comment, null).ConfigureAwait(true);

            if (GetPlaceholder() == null)
            {
                timeline.Add(vm);
            }
            else
            {
                timeline.Insert(timeline.Count - 1, vm);
            }
        }

        void ClearPlaceholder()
        {
            var placeholder = GetPlaceholder();

            if (placeholder != null)
            {
                placeholder.Body = null;
            }
        }

        ICommentViewModel GetPlaceholder()
        {
            if (timeline.Count > 0 &&
                timeline[timeline.Count - 1] is ICommentViewModel comment &&
                comment.Id == null)
            {
                return comment;
            }

            return null;
        }

        async Task DoShowCommit(string oid)
        {
            await service.FetchCommit(LocalRepository, Repository, oid).ConfigureAwait(true);
            teServices.ShowCommitDetails(oid);
        }
    }
}
