﻿using System;
using System.Collections.Generic;
using System.Text;
using FreeSql.DataAnnotations;

namespace LinCms.Zero.Domain.Blog
{
    [Table(Name = "blog_user_like")]
    public class UserLike : FullAduitEntity<Guid>
    {
        public Guid SubjectId { get; set; }

        /// <summary>
        /// 点赞对象 1 是文章，2 是评论
        /// </summary>
        public int SubjectType { get; set; }
        [Navigate("CreateUserId")]
        public virtual LinUser LinUser { get; set; }

        [Navigate("SubjectId")]
        public virtual Comment Comment { get; set; }
    }
}