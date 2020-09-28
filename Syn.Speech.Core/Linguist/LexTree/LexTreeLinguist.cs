﻿using System;
using Syn.Speech.Linguist.Acoustic;
using Syn.Speech.Linguist.Dictionary;
using Syn.Speech.Linguist.Language.Grammar;
using Syn.Speech.Linguist.Language.NGram;
using Syn.Speech.Linguist.Language.NGram.Large;
using Syn.Speech.Linguist.Util;
using Syn.Speech.Util;
using Syn.Speech.Util.Props;
//PATROLLED + REFACTORED
namespace Syn.Speech.Linguist.LexTree
{
    /// <summary>
    /// A linguist that can represent large vocabularies efficiently. This class implements the Linguist interface. The main
    /// role of any linguist is to represent the search space for the decoder. The initial state in the search space can be
    /// retrieved by a SearchManager via a call to <code> getInitialSearchState</code>. This method returns a SearchState.
    /// Successor states can be retrieved via calls to <code>SearchState.getSuccessors().</code>. There are a number of
    /// search state sub-interfaces that are used to indicate different types of states in the search space:
    /// <p/>
    /// <ul> <li><b>WordSearchState </b>- represents a word in the search space. <li><b>UnitSearchState </b>- represents a
    /// unit in the search space <li><b>HMMSearchState </b> represents an HMM state in the search space </ul>
    /// <p/>
    /// A linguist has a great deal of latitude about the order in which it returns states. For instance a 'flat' linguist
    /// may return a WordState at the beginning of a word, while a 'tree' linguist may return WordStates at the ending of a
    /// word. Likewise, a linguist may omit certain state types completely (such as a unit state). Some Search Managers may
    /// want to know a priori the order in which states will be generated by the linguist. The method
    /// <code>getSearchStateOrder</code> can be used to retrieve the order of state returned by the linguist.
    /// <p/>
    /// <p/>
    /// Depending on the vocabulary size and topology, the search space represented by the linguist may include a very large
    /// number of states. Some linguists will generate the search states dynamically, that is, the object representing a
    /// particular state in the search space is not created until it is needed by the SearchManager. SearchManagers often
    /// need to be able to determine if a particular state has been entered before by comparing states. Because SearchStates
    /// may be generated dynamically, the <code>SearchState.equals()</code> call (as opposed to the reference equals '=='
    /// method) should be used to determine if states are equal. The states returned by the linguist will generally provide
    /// very efficient implementations of <code>equals</code> and <code>hashCode</code>. This will allow a SearchManager to
    /// maintain collections of states in HashMaps efficiently.
    /// <p/>
    /// <p/>
    /// <p/>
    /// <b>LexTeeLinguist Characteristics </b>
    /// <p/>
    /// Some characteristics of this linguist: <ul> <li><b>Dynamic </b>- the linguist generates search states on the fly,
    /// greatly reducing the required memory footprint <li><b>tree topology </b> this linguist represents the search space as
    /// an inverted tree. Units near the roots of word are shared among many different words. These reduces the amount of
    /// states that need to be considered during the search. <li><b>HMM sharing </b>- because of state tying in the acoustic
    /// models, it is often the case that triphone units that differ in the right context actually are represented by the
    /// same HMM. This linguist recognizes this case and will use a single state to represent the HMM instead of two states.
    /// This can greatly reduce the number of states generated by the linguist. <li><b>Small-footprint </b>- this linguist
    /// uses a few other techniques to reduce the overall footprint of the search space. One technique that is particularly
    /// helpful is to share the end word units (where the largest fanout of states occurs) across all of the words. For a 60K
    /// word vocabulary, these can result in a reduction in tree nodes of about 2 million to around 3,000. <li><b>Quick
    /// loading </b>- this linguist can compile the search space very quickly. A 60K word vocabulary can be made ready in
    /// less than 10 seconds. </ul>
    /// <p/>
    /// This linguist is not a general purpose linguist. It does impose some constraints:
    /// <p/>
    /// <ul> <li><b>unit size </b>- this linguist will units that are no larger than triphones. <li><b>n-gram grammars </b>-
    /// this linguist will generate the search space directly from the N-Gram language model. The vocabulary supported is the
    /// intersection of the words found in the language model and the words that exist in the Dictionary. It is assumed that
    /// all sequences of words in the vocabulary are valid. This linguist doesn't support arbitrary grammars. </ul>
    /// <p/>
    /// <p/>
    /// <b>Design Notes </b> The following are some notes describing the design of this linguist. They may be helpful to
    /// those who want to understand how this linguist works but are not necessary if you are only interested in using this
    /// linguist.
    /// <p/>
    /// <p/>
    /// <p/>
    /// <b>Search Space Representation </b> It has been shown that representing the search space as a tree can greatly reduce
    /// the number of active states in a search since the units at the beginnings of words can be shared across multiple
    /// words. For example, with a large vocabulary (60K words), at the end of a word, with a flat representation, we have to
    /// provide transitions to the initial state of each possible word. That is 60K transitions. In a tree based system we
    /// need to only provide transitions to each initial phone (within its context). That is about 1600 transitions. This is
    /// a substantial reduction. Conceptually, this tree consists of a node for each possible initial unit. Each node can
    /// have an arbitrary number of children which can be either unit nodes or word nodes.
    /// <p/>
    /// <p/>
    /// This linguist uses the HMMTree class to build and represent the tree. The HMMTree is given the dictionary and
    /// language model and builds the lextree. Instead of representing the nodes in the tree as phonemes and words as is
    /// typically done, the HMMTree represents the tree as HMMs and words. The HMM is essentially a unit within its context.
    /// This is typically a triphone (although for some units (such as SIL) it is a simple phone. Representing the nodes as
    /// HMM instead of nodes yields a much larger tree, but also has some advantages:
    /// <p/>
    /// <ul> <li>Because of state-tying in the acoustic models, many distinct triphones actually share an HMM. Representing
    /// the nodes as HMMs allows these shared HMMs to be represented in the tree only once instead of many times if we
    /// representing states as phones or triphones. This leads to a reduction in the actual number of states that are
    /// considered during a search. Experiments have shown that this can reduce the required beam by a factor of 2 or 3.
    /// <li>By representing the nodes as HMM, we avoid having to lookup the HMM for a particular triphone during the search.
    /// This is a modest savings. </ul>
    /// <p/>
    /// There are some disadvantages in representing the tree with HMMs:
    /// <p/>
    /// <ul> <li><b>size</b> since HMMs represent units in their context, we have many more copies of each node. For
    /// instance, instead of having a single unit representing the initial 'd' in the word 'dog' we would have about 40 HMMs,
    /// one for each possible left context. <li><b>speed </b> building the much larger HMM tree can take much more time,
    /// since many more nodes are needed to represent the tree. <li><b>complexity </b> representing the tree with HMMs is
    /// more complex. There are multiple entry points for each word/unit that have to be dealt with. </ul>
    /// <p/>
    /// Luckily the size and speed issues can be mitigated (by adding a bit more complexity of course). The bulk of the nodes
    /// in the HMM tree are the word ending nodes. There is a word ending node for each possible right context. To reduce
    /// space, all of the word ending nodes are replaced by a single EndNode. During the search, the actual HMM nodes for a
    /// particular EndNode are generated on request. These sets of HMM nodes can be shared among different word endings, and
    /// therefore are cached. The effect of using this EndNode optimization is to reduce the space required by the tree by
    /// about 300mb and the time required to generate the tree from about 60 seconds to about 6 seconds.
    ///
    /// <p/>
    /// <b>Word Histories </b>
    /// <p/>
    /// We use explicit backoff for word histories. That technique is proven to be useful and save number of
    /// states. The reasoning is the following. With a vocabulary of size N, you have N^2 unique bigram
    /// histories. So the token stack will have N^2*K unique tokens, where K is the number of states per token.
    /// For a 100k vocab, 3 states per HMM, that will be 3*10^10 tokens (max). Of course, a large majority
    /// of them will be pruned, but really, its still way too much. If you stick with the <b>actual</b>  K-gram
    /// used (i.e. accounting explicitly for backoff), then this reduces <b>tremendously</b>.
    /// Most bigrams dont have corresponding trigrams.  Not all 10^10 bigrams have trigrams. We only
    /// need to store as many explicit tokens as the number of bigrams that have trigrams.
    /// <summary>
    public class LexTreeLinguist  : Linguist
    {
        /** The property that defines the grammar to use when building the search graph */
        [S4Component(Type = typeof(Grammar))]
        public static string PropGrammar = "grammar";

        /** The property that defines the acoustic model to use when building the search graph */
        [S4Component(Type = typeof(AcousticModel))]
        public static string PropAcousticModel = "acousticModel";

        /** The property that defines the unit manager to use when building the search graph */
        [S4Component(Type = typeof(UnitManager), DefaultClass = typeof(UnitManager))]
        public static string PropUnitManager = "unitManager";

        /**
        /// The property that determines whether or not full word histories are used to
        /// determine when two states are equal.
         */
        [S4Boolean(DefaultValue = true)]
        public static string PropFullWordHistories = "fullWordHistories";

        /** The property for the language model to be used by this grammar */
        [S4Component(Type = typeof(LanguageModel))]
        public static string PropLanguageModel = "languageModel";

        /** The property that defines the dictionary to use for this grammar */
        [S4Component(Type = typeof(IDictionary))]
        public static string PropDictionary = "dictionary";

        /** The property that defines the size of the arc cache (zero to disable the cache). */
        [S4Integer(DefaultValue = 0)]
        public static string PropCacheSize = "cacheSize";

        /** The property that controls whether filler words are automatically added to the vocabulary */
        [S4Boolean(DefaultValue = false)]
        public static string PropAddFillerWords = "addFillerWords";

        /**
        /// The property to control whether or not the linguist will generate unit states.   When this property is false the
        /// linguist may omit UnitSearchState states.  For some search algorithms this will allow for a faster search with
        /// more compact results.
         */
        [S4Boolean(DefaultValue = false)]
        public static string PropGenerateUnitStates = "generateUnitStates";

        /**
        /// The property that determines whether or not unigram probabilities are
        /// smeared through the lextree. During the expansion of the tree the
        /// language probability could be only calculated when we reach word end node.
        /// Until that point we need to keep path alive and give it some language
        /// probability. See
         *
        /// Alleva, F., Huang, X. & Hwang, M.-Y., "Improvements on the pronunciation
        /// prefix tree search organization", Proceedings of ICASSP, pp. 133-136,
        /// Atlanta, GA, 1996.
         *
        /// for the description of this technique.
         */
        [S4Boolean(DefaultValue = true)]
        public static string PropWantUnigramSmear = "wantUnigramSmear";


        /** The property that determines the weight of the smear. See {@link LexTreeLinguist#PROP_WANT_UNIGRAM_SMEAR} */
        [S4Double(DefaultValue = 1.0)]
        public static string PropUnigramSmearWeight = "unigramSmearWeight";


        // just for detailed debugging
        public  static ISearchStateArc[] EmptyArc = new ISearchStateArc[0];

        // ----------------------------------
        // Subcomponents that are configured
        // by the property sheet
        // -----------------------------------
        private AcousticModel _acousticModel;
        private LogMath _logMath;
        private UnitManager _unitManager;

        // ------------------------------------
        // Data that is configured by the
        // property sheet
        // ------------------------------------
        //public Boolean fullWordHistories = true;
        protected Boolean AddFillerWords;
        public Boolean GenerateUnitStates;
        private Boolean _wantUnigramSmear = true;
        private float _unigramSmearWeight = 1.0f;
        public Boolean CacheEnabled;
        private int _maxArcCacheSize;

        public float LanguageWeight;
        private float _logWordInsertionProbability;
        private float _logUnitInsertionProbability;
        private float _logFillerInsertionProbability;
        private float _logSilenceInsertionProbability;
        public float LogOne;

        // ------------------------------------
        // Data used for building and maintaining
        // the search graph
        // -------------------------------------
        public Word SentenceEndWord;
        private Word[] _sentenceStartWordArray;
        private ISearchGraph _searchGraph;
        private HMMPool _hmmPool;
        public LRUCache<LexTreeState, ISearchStateArc[]> ArcCache;
        public int MaxDepth;

        public HMMTree HMMTree;

        public int CacheTrys;
        public int CacheHits;

        public LexTreeLinguist(AcousticModel acousticModel, UnitManager unitManager,
                LanguageModel languageModel, IDictionary dictionary, Boolean fullWordHistories, Boolean wantUnigramSmear,
                double wordInsertionProbability, double silenceInsertionProbability, double fillerInsertionProbability,
                double unitInsertionProbability, float languageWeight, Boolean addFillerWords, Boolean generateUnitStates,
                float unigramSmearWeight, int maxArcCacheSize) 
        {


            _acousticModel = acousticModel;
            _logMath = LogMath.GetLogMath();
            _unitManager = unitManager;
            LanguageModel = languageModel;
            Dictionary = dictionary;

            //this.fullWordHistories = fullWordHistories;
            _wantUnigramSmear = wantUnigramSmear;
            _logWordInsertionProbability = _logMath.LinearToLog(wordInsertionProbability);
            _logSilenceInsertionProbability = _logMath.LinearToLog(silenceInsertionProbability);
            _logFillerInsertionProbability = _logMath.LinearToLog(fillerInsertionProbability);
            _logUnitInsertionProbability = _logMath.LinearToLog(unitInsertionProbability);
            LanguageWeight = languageWeight;
            AddFillerWords = addFillerWords;
            GenerateUnitStates = generateUnitStates;
            _unigramSmearWeight = unigramSmearWeight;
            _maxArcCacheSize = maxArcCacheSize;

            CacheEnabled = maxArcCacheSize > 0;
            if( CacheEnabled ) {
                ArcCache = new LRUCache<LexTreeState, ISearchStateArc[]>(maxArcCacheSize);
            }
        }

        public LexTreeLinguist() {

        }

        /*
       /// (non-Javadoc)
        *
       /// @see edu.cmu.sphinx.util.props.Configurable#newProperties(edu.cmu.sphinx.util.props.PropertySheet)
        */
        public override void NewProperties(PropertySheet ps)
        {
            _logMath = LogMath.GetLogMath();

            _acousticModel = (AcousticModel) ps.GetComponent(PropAcousticModel);
            _unitManager = (UnitManager) ps.GetComponent(PropUnitManager);
            LanguageModel = (LanguageModel) ps.GetComponent(PropLanguageModel);
            Dictionary = (IDictionary) ps.GetComponent(PropDictionary);

            //fullWordHistories = ps.getBoolean(PROP_FULL_WORD_HISTORIES);
            _wantUnigramSmear = ps.GetBoolean(PropWantUnigramSmear);
            _logWordInsertionProbability = _logMath.LinearToLog(ps.GetDouble(PropWordInsertionProbability));
            _logSilenceInsertionProbability = _logMath.LinearToLog(ps.GetDouble(PropSilenceInsertionProbability));
            _logFillerInsertionProbability = _logMath.LinearToLog(ps.GetDouble(PropFillerInsertionProbability));
            _logUnitInsertionProbability = _logMath.LinearToLog(ps.GetDouble(PropUnitInsertionProbability));
            LanguageWeight = ps.GetFloat(PropLanguageWeight);
            AddFillerWords = (ps.GetBoolean(PropAddFillerWords));
            GenerateUnitStates = (ps.GetBoolean(PropGenerateUnitStates));
            _unigramSmearWeight = ps.GetFloat(PropUnigramSmearWeight);
            _maxArcCacheSize = ps.GetInt(PropCacheSize);

            CacheEnabled = _maxArcCacheSize > 0;
            if(CacheEnabled) {
                ArcCache = new LRUCache<LexTreeState, ISearchStateArc[]>(_maxArcCacheSize);
            }
        }


        /*
       /// (non-Javadoc)
        *
       /// @see edu.cmu.sphinx.linguist.Linguist#allocate()
        */
        public override void Allocate()
        {
            Dictionary.Allocate();
            _acousticModel.Allocate();
            LanguageModel.Allocate();
            CompileGrammar();
        }


        /*
       /// (non-Javadoc)
        *
       /// @see edu.cmu.sphinx.linguist.Linguist#deallocate()
        */
        public override void Deallocate()
        {
            if (_acousticModel != null)
                _acousticModel.Deallocate();
            if (Dictionary != null)
                Dictionary.Deallocate();
            if (LanguageModel != null)
                LanguageModel.Deallocate();
            HMMTree = null;
        }


        /*
       /// (non-Javadoc)
        *
       /// @see edu.cmu.sphinx.linguist.Linguist#getSearchGraph()
        */

        public override ISearchGraph SearchGraph
        {
            get { return _searchGraph; }
        }


        /** Called before a recognition */
        public override void StartRecognition() 
        {
        }


        /** Called after a recognition */
        public override void StopRecognition() 
        {
             //   FIXME: remove
            if (LanguageModel is LargeNGramModel)
                ((LargeNGramModel) LanguageModel).Stop();
        }


        /**
           /// Retrieves the language model for this linguist
            *
           /// @return the language model (or null if there is none)
            */

        public LanguageModel LanguageModel { get; set; }


        public IDictionary Dictionary { get; private set; }


        /**
           /// retrieves the initial language state
            *
           /// @return the initial language state
            */
        private ISearchState GetInitialSearchState() 
        {
            InitialWordNode node = HMMTree.InitialNode;

            if (node == null)
                throw new SystemException("Language model has no entry for initial word <s>");

            return new LexTreeWordState(node, node.Parent, (new WordSequence(_sentenceStartWordArray)).Trim(MaxDepth - 1)
                , 0f, LogOne, LogOne,this);
        }


        /** Compiles the n-gram into a lex tree that is used during the search */
        private void CompileGrammar() 
        {
            TimerPool.GetTimer(this, "Compile").Start();

            SentenceEndWord = Dictionary.GetSentenceEndWord();
            _sentenceStartWordArray = new Word[1];
            _sentenceStartWordArray[0] = Dictionary.GetSentenceStartWord();
            MaxDepth = LanguageModel.MaxDepth;

            GenerateHmmTree();

            TimerPool.GetTimer(this,"Compile").Stop();
            //    Now that we are all done, dump out some interesting
            //    information about the process

            _searchGraph = new LexTreeSearchGraph(GetInitialSearchState());
        }


        protected void GenerateHmmTree() 
        {
            _hmmPool = new HMMPool(_acousticModel, _unitManager);
            HMMTree = new HMMTree(_hmmPool, Dictionary, LanguageModel, AddFillerWords, LanguageWeight);


            _hmmPool.DumpInfo();
        }
       
        /**
           /// Determines the insertion probability for the given unit lex node
            *
           /// @param unitNode the unit lex node
           /// @return the insertion probability
            */
        public float CalculateInsertionProbability(UnitNode unitNode)
        {
            int type = unitNode.Type;

            if (type == UnitNode.SimpleUnit) 
            {
                return _logUnitInsertionProbability;
            }
            if( type == UnitNode.WordBeginningUnit)
            {
                return _logUnitInsertionProbability + _logWordInsertionProbability;
            }
            if (type == UnitNode.SilenceUnit)
            {
                return _logSilenceInsertionProbability;
            }
            // must be filler
            return _logFillerInsertionProbability;
        }


        /**
           /// Retrieves the unigram smear from the given node
            *
           /// @return the unigram smear
            */
        public float GetUnigramSmear(Node node) 
        {
            float prob;
            if (_wantUnigramSmear) 
            {
                prob = node.UnigramProbability * _unigramSmearWeight;
            } 
            else 
            {
                prob = LogOne;
            }
            return prob;
        }


        /**
           /// Returns the smear term for the given word sequence
            *
           /// @param ws the word sequence
           /// @return the smear term for the word sequence
            */
        public float GetSmearTermFromLanguageModel(WordSequence ws) 
        {
            return LanguageModel.GetSmear(ws);
        }


        /**
           /// Gets the set of HMM nodes associated with the given end node
            *
           /// @param endNode the end node
           /// @return an array of associated HMM nodes
            */
        public HMMNode[] GetHMMNodes(EndNode endNode) 
        {
            return HMMTree.GetHMMNodes(endNode);
        }

    }
}
