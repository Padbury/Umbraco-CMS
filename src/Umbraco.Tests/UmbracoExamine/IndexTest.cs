﻿using System;
using System.Collections.Generic;
using System.Linq;
using Examine;
using Examine.LuceneEngine;
using Examine.LuceneEngine.Providers;
using Examine.LuceneEngine.SearchCriteria;
using Examine.SearchCriteria;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using NUnit.Framework;
using Umbraco.Tests.TestHelpers;
using UmbracoExamine;

namespace Umbraco.Tests.UmbracoExamine
{

    /// <summary>
    /// Tests the standard indexing capabilities
    /// </summary>
    [DatabaseTestBehavior(DatabaseBehavior.NewDbFileAndSchemaPerTest)]
    [TestFixture, RequiresSTA]
    public class IndexTest : ExamineBaseTest
    {
        /// <summary>
        /// Check that the node signalled as protected in the content service is not present in the index.
        /// </summary>
        [Test]
        public void Index_Protected_Content_Not_Indexed()
        {

            using (var luceneDir = new RAMDirectory())
            using (var indexer = IndexInitializer.GetUmbracoIndexer(luceneDir))
            using (var searcher = IndexInitializer.GetUmbracoSearcher(luceneDir))
            {
                indexer.RebuildIndex();

                var protectedQuery = new BooleanQuery();
                protectedQuery.Add(
                    new BooleanClause(
                        new TermQuery(new Term(LuceneIndexer.IndexTypeFieldName, IndexTypes.Content)),
                        BooleanClause.Occur.MUST));

                protectedQuery.Add(
                    new BooleanClause(
                        new TermQuery(new Term(LuceneIndexer.IndexNodeIdFieldName, TestContentService.ProtectedNode.ToString())),
                        BooleanClause.Occur.MUST));

                var collector = new AllHitsCollector(false, true);
                var s = searcher.GetSearcher();
                s.Search(protectedQuery, collector);

                Assert.AreEqual(0, collector.Count, "Protected node should not be indexed");
            }

        }

        [Test]
        public void Index_Move_Media_From_Non_Indexable_To_Indexable_ParentID()
        {
            using (var luceneDir = new RAMDirectory())
            using (var indexer = IndexInitializer.GetUmbracoIndexer(luceneDir))
            using (var searcher = IndexInitializer.GetUmbracoSearcher(luceneDir))
            {
                indexer.RebuildIndex();

                var mediaService = new TestMediaService();

                //change parent id to 1116
                var existingCriteria = indexer.IndexerData;
                indexer.IndexerData = new IndexCriteria(existingCriteria.StandardFields, existingCriteria.UserFields, existingCriteria.IncludeNodeTypes, existingCriteria.ExcludeNodeTypes,
                    1116);

                //rebuild so it excludes children unless they are under 1116
                indexer.RebuildIndex();

                //ensure that node 2112 doesn't exist
                var results = searcher.Search(searcher.CreateSearchCriteria().Id(2112).Compile());
                Assert.AreEqual(0, results.Count());

                //get a node from the data repo (this one exists underneath 2222)
                var node = mediaService.GetLatestMediaByXpath("//*[string-length(@id)>0 and number(@id)>0]")
                                        .Root
                                        .Elements()
                                        .First(x => (int)x.Attribute("id") == 2112);

                var currPath = (string)node.Attribute("path"); //should be : -1,1111,2222,2112
                Assert.AreEqual("-1,1111,2222,2112", currPath);

                //now mimic moving 2112 to 1116
                //node.SetAttributeValue("path", currPath.Replace("2222", "1116"));
                node.SetAttributeValue("path", "-1,1116,2112");
                node.SetAttributeValue("parentID", "1116");

                //now reindex the node, this should first delete it and then WILL add it because of the parent id constraint
                indexer.ReIndexNode(node, IndexTypes.Media);

                //RESET the parent id
                existingCriteria = ((IndexCriteria)indexer.IndexerData);
                indexer.IndexerData = new IndexCriteria(existingCriteria.StandardFields, existingCriteria.UserFields, existingCriteria.IncludeNodeTypes, existingCriteria.ExcludeNodeTypes,
                    null);

                //now ensure it's deleted
                var newResults = searcher.Search(searcher.CreateSearchCriteria().Id(2112).Compile());
                Assert.AreEqual(1, newResults.Count());
            }

            
        }

        [Test]
        [Ignore]
        public void Index_Move_Media_To_Non_Indexable_ParentID()
        {
            using (var luceneDir = new RAMDirectory())
            using (var indexer = IndexInitializer.GetUmbracoIndexer(luceneDir))
            using (var searcher = IndexInitializer.GetUmbracoSearcher(luceneDir))
            {
                indexer.RebuildIndex();

                var mediaService = new TestMediaService();

                //get a node from the data repo (this one exists underneath 2222)
                var node = mediaService.GetLatestMediaByXpath("//*[string-length(@id)>0 and number(@id)>0]")
                                    .Root
                                    .Elements()
                                    .First(x => (int)x.Attribute("id") == 2112);

                var currPath = (string)node.Attribute("path"); //should be : -1,1111,2222,2112
                Assert.AreEqual("-1,1111,2222,2112", currPath);

                //ensure it's indexed
                indexer.ReIndexNode(node, IndexTypes.Media);

                //change the parent node id to be the one it used to exist under
                var existingCriteria = indexer.IndexerData;
                indexer.IndexerData = new IndexCriteria(existingCriteria.StandardFields, existingCriteria.UserFields, existingCriteria.IncludeNodeTypes, existingCriteria.ExcludeNodeTypes,
                    2222);

                //now mimic moving the node underneath 1116 instead of 2222
                node.SetAttributeValue("path", currPath.Replace("2222", "1116"));
                node.SetAttributeValue("parentID", "1116");

                //now reindex the node, this should first delete it and then NOT add it because of the parent id constraint
                indexer.ReIndexNode(node, IndexTypes.Media);

                //RESET the parent id
                existingCriteria = ((IndexCriteria)indexer.IndexerData);
                indexer.IndexerData = new IndexCriteria(existingCriteria.StandardFields, existingCriteria.UserFields, existingCriteria.IncludeNodeTypes, existingCriteria.ExcludeNodeTypes,
                    null);

                //now ensure it's deleted
                var results = searcher.Search(searcher.CreateSearchCriteria().Id(2112).Compile());
                Assert.AreEqual(0, results.Count());
            }
        }


        /// <summary>
        /// This will ensure that all 'Content' (not media) is cleared from the index using the Lucene API directly.
        /// We then call the Examine method to re-index Content and do some comparisons to ensure that it worked correctly.
        /// </summary>
        [Test]
        public void Index_Reindex_Content()
        {
            using (var luceneDir = new RAMDirectory())
            using (var indexer = IndexInitializer.GetUmbracoIndexer(luceneDir, supportUnpublishedContent:true))
            using (var searcher = IndexInitializer.GetUmbracoSearcher(luceneDir))
            {
                indexer.RebuildIndex();

                var s = (IndexSearcher)searcher.GetSearcher();

                //first delete all 'Content' (not media). This is done by directly manipulating the index with the Lucene API, not examine!

                var contentTerm = new Term(LuceneIndexer.IndexTypeFieldName, IndexTypes.Content);
                var writer = indexer.GetIndexWriter();
                writer.DeleteDocuments(contentTerm);
                writer.Commit();
                
                //make sure the content is gone. This is done with lucene APIs, not examine!
                var collector = new AllHitsCollector(false, true);
                var query = new TermQuery(contentTerm);
                s = (IndexSearcher)searcher.GetSearcher(); //make sure the searcher is up do date.
                s.Search(query, collector);
                Assert.AreEqual(0, collector.Count);

                //call our indexing methods
                indexer.IndexAll(IndexTypes.Content);

                collector = new AllHitsCollector(false, true);
                s = (IndexSearcher)searcher.GetSearcher(); //make sure the searcher is up do date.
                s.Search(query, collector);
                //var ids = new List<string>();
                //for (var i = 0; i < collector.Count;i++)
                //{
                //    ids.Add(s.Doc(collector.GetDocId(i)).GetValues("__NodeId")[0]);
                //}
                Assert.AreEqual(21, collector.Count);
            }
        }

        /// <summary>
        /// This will delete an item from the index and ensure that all children of the node are deleted too!
        /// </summary>
        [Test]
        [Ignore]
        public void Index_Delete_Index_Item_Ensure_Heirarchy_Removed()
        {

            using (var luceneDir = new RAMDirectory())
            using (var indexer = IndexInitializer.GetUmbracoIndexer(luceneDir))
            using (var searcher = IndexInitializer.GetUmbracoSearcher(luceneDir))
            {
                indexer.RebuildIndex();

                //now delete a node that has children

                indexer.DeleteFromIndex(1140.ToString());
                //this node had children: 1141 & 1142, let's ensure they are also removed

                var results = searcher.Search(searcher.CreateSearchCriteria().Id(1141).Compile());
                Assert.AreEqual(0, results.Count());

                results = searcher.Search(searcher.CreateSearchCriteria().Id(1142).Compile());
                Assert.AreEqual(0, results.Count());
            }
        }
        
        #region Initialize and Cleanup
            
        public override void TearDown()
        {
            base.TearDown();
        
            UmbracoExamineSearcher.DisableInitializationCheck = null;
            BaseUmbracoIndexer.DisableInitializationCheck = null;
        }
        
        #endregion
    }
}